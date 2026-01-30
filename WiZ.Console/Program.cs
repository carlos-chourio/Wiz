using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using WiZ;
using WiZ.Models;
using WiZ.Services;
using WiZ.Helpers;

namespace WiZ.Console
{
    class Program
    {
        private const string TestBulbConfigFile = "test_bulb_config.json";
        private static BulbService bulbService;

        static async Task Main(string[] args)
        {
            // 1. Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                // 2. Set up Dependency Injection
                var services = new ServiceCollection();
                ConfigureServices(services);
                var serviceProvider = services.BuildServiceProvider();

                // 3. Resolve BulbService (This automatically injects ILogger and UdpCommunicationService)
                bulbService = serviceProvider.GetRequiredService<BulbService>();

                System.Console.WriteLine("=== WiZ Bulb Testing Console ===");
                System.Console.WriteLine();

                var testBulbConfig = LoadTestBulbConfig();
                BulbModel? testBulb = null;

                if (testBulbConfig == null)
                {
                    System.Console.WriteLine("No test bulb configured. Starting discovery...");
                    testBulb = await DiscoverAndSelectBulb();

                    if (testBulb != null)
                    {
                        await SaveTestBulbConfig(testBulb);
                        System.Console.WriteLine($"Test bulb saved: {testBulb.MACAddress}");
                    }
                    else
                    {
                        System.Console.WriteLine("No bulbs found. Exiting.");
                        return;
                    }
                }
                else
                {
                    System.Console.WriteLine($"Using saved test bulb: {testBulbConfig.MacAddress}");
                    testBulb = await bulbService.GetBulbByMacAsync(MACAddress.Parse(testBulbConfig.MacAddress));
                    if (testBulb == null)
                    {
                        System.Console.WriteLine("Failed to connect to saved bulb. Running discovery...");
                        testBulb = await DiscoverAndSelectBulb();
                        if (testBulb == null)
                        {
                            System.Console.WriteLine("No bulbs found. Exiting.");
                            return;
                        }
                    }
                }

                await TestBulbInteractions(testBulb);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Ensure logs are written before closing
                await Log.CloseAndFlushAsync();
            }

            System.Console.WriteLine("\nPress any key to exit...");
            System.Console.Read();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add Logging to DI
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            // Register Services
            services.AddSingleton<UdpCommunicationService>();
            
            // Manual registration for BulbService because of the '5000' timeout parameter
            services.AddSingleton<BulbService>(sp =>
            {
                var udp = sp.GetRequiredService<UdpCommunicationService>();
                var logger = sp.GetRequiredService<ILogger<BulbService>>();
                return new BulbService(udp, 5000, logger);
            });
        }

        private static async Task<BulbModel?> DiscoverAndSelectBulb()
        {
            System.Console.WriteLine("Discovering WiZ bulbs on your network...");
            System.Console.WriteLine("This will scan for 10 seconds. Please wait...");
            System.Console.WriteLine();

            var localAddr = WiZ.NetworkHelper.DefaultLocalIP;
            var macAddr = WiZ.NetworkHelper.DefaultLocalMAC;

            var discoveredBulbs = await bulbService.ScanForBulbsAsync(
                localAddr,
                macAddr,
                WiZ.ScanMode.GetSystemConfig,
                10000,
                (bulb) =>
                {
                    System.Console.WriteLine($"Found: {bulb.MACAddress} - {bulb.IPAddress} - {bulb.BulbType}");
                });

            System.Console.WriteLine();
            System.Console.WriteLine($"Discovery complete. Found {discoveredBulbs.Count} bulb(s).");

            if (discoveredBulbs.Count == 0)
            {
                return null;
            }
            else if (discoveredBulbs.Count == 1)
            {
                System.Console.WriteLine("Auto-selecting the only bulb found.");
                return discoveredBulbs.First();
            }
            else
            {
                System.Console.WriteLine("Multiple bulbs found. Please select one:");
                for (int i = 0; i < discoveredBulbs.Count; i++)
                {
                    var bulb = discoveredBulbs[i];
                    System.Console.WriteLine($"{i + 1}. {bulb.MACAddress} - {bulb.IPAddress} - {bulb.BulbType}");
                }

                System.Console.Write("Enter bulb number (or press Enter for first bulb): ");
                var input = System.Console.ReadLine();

                if (int.TryParse(input, out int selection) && selection > 0 && selection <= discoveredBulbs.Count)
                {
                    return discoveredBulbs[selection - 1];
                }

                return discoveredBulbs.First();
            }
        }

        private static async Task TestBulbInteractions(BulbModel bulb)
        {
            System.Console.WriteLine("\n=== Testing Bulb Interactions ===");
            System.Console.WriteLine($"Bulb: {bulb.MACAddress} - {bulb.IPAddress}");
            System.Console.WriteLine();

            System.Console.WriteLine("Testing connection and getting current state...");
            try
            {
                await bulbService.GetPilotAsync(bulb);
                System.Console.WriteLine($"Connection successful!");
                System.Console.WriteLine($"Current state: {(bulb.IsPoweredOn == true ? "ON" : "OFF")}");
                System.Console.WriteLine($"Brightness: {bulb.Settings?.Brightness ?? 0}%");
                System.Console.WriteLine($"Scene ID: {bulb.Settings?.Scene ?? 0}");
                System.Console.WriteLine($"Temperature: {bulb.Settings?.Temperature ?? 0}K");
                System.Console.WriteLine($"RGB: R={bulb.Settings?.Red ?? 0}, G={bulb.Settings?.Green ?? 0}, B={bulb.Settings?.Blue ?? 0}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to get bulb state: {ex.Message}");
                return;
            }

            System.Console.WriteLine();

            await TestBasicControls(bulb);
            await TestStateCaching(bulb);
            await TestUdpBehavior(bulb);
        }

        private static async Task TestBasicControls(BulbModel bulb)
        {
            System.Console.WriteLine("=== Testing Basic Controls ===");

            try
            {
                System.Console.WriteLine("Turning bulb ON...");
                await bulbService.TurnOnAsync(bulb);
                await Task.Delay(2000);

                System.Console.WriteLine("Setting brightness to 50%...");
                await bulbService.SetBrightnessAsync(bulb, 50);
                await Task.Delay(2000);

                System.Console.WriteLine("Turning bulb OFF...");
                await bulbService.TurnOffAsync(bulb);
                await Task.Delay(2000);

                System.Console.WriteLine("Basic controls test completed successfully!");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in basic controls test: {ex.Message}");
            }

            System.Console.WriteLine();
        }

        private static async Task TestStateCaching(BulbModel bulb)
        {
            System.Console.WriteLine("=== Testing State Caching ===");

            try
            {
                await bulbService.TurnOnAsync(bulb);
                await Task.Delay(1000);

                var initialState = bulb.IsPoweredOn;
                System.Console.WriteLine($"State after turning ON: {initialState}");

                await bulbService.GetPilotAsync(bulb);
                var refreshedState = bulb.IsPoweredOn;
                System.Console.WriteLine($"State after refresh: {refreshedState}");

                if (initialState == refreshedState && refreshedState == true)
                {
                    System.Console.WriteLine("✓ State caching is working correctly");
                }
                else
                {
                    System.Console.WriteLine("✗ State caching issue detected!");
                    System.Console.WriteLine($"Expected: true, Got: {refreshedState}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in state caching test: {ex.Message}");
            }

            System.Console.WriteLine();
        }

        private static async Task TestUdpBehavior(BulbModel bulb)
        {
            System.Console.WriteLine("=== Testing UDP Behavior (Simulating Debug Scenario) ===");

            try
            {
                System.Console.WriteLine("Testing multiple rapid commands (simulating breakpoint scenario)...");

                var tasks = new List<Task>();
                for (int i = 0; i < 5; i++)
                {
                    int taskNum = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            System.Console.WriteLine($"Task {taskNum}: Getting state...");
                            await bulbService.GetPilotAsync(bulb);
                            System.Console.WriteLine($"Task {taskNum}: Success");
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Task {taskNum}: Failed - {ex.Message}");
                        }
                    }));

                    await Task.Delay(100);
                }

                await Task.WhenAll(tasks);
                System.Console.WriteLine("UDP behavior test completed!");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in UDP behavior test: {ex.Message}");
            }

            System.Console.WriteLine();
        }

        private static TestBulbConfig? LoadTestBulbConfig()
        {
            try
            {
                if (File.Exists(TestBulbConfigFile))
                {
                    var json = File.ReadAllText(TestBulbConfigFile);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<TestBulbConfig>(json);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Warning: Could not load test bulb config: {ex.Message}");
            }

            return null;
        }

        private static async Task SaveTestBulbConfig(BulbModel bulb)
        {
            try
            {
                var config = new TestBulbConfig
                {
                    MacAddress = bulb.MACAddress.ToString(),
                    IPAddress = bulb.IPAddress.ToString(),
                    ModelName = bulb.BulbType,
                    LastSeen = DateTime.Now
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                await File.WriteAllTextAsync(TestBulbConfigFile, json);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Warning: Could not save test bulb config: {ex.Message}");
            }
        }
    }

    public class TestBulbConfig
    {
        public string MacAddress { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; }
    }
}