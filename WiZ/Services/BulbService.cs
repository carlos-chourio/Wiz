using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WiZ.Models;
using WiZ.Profiles;
using Microsoft.Extensions.Logging;
using WiZ.Helpers;

namespace WiZ.Services
{
    /// <summary>
    /// Service for handling WiZ bulb operations and communication.
    /// This service handles all bulb actions while BulbModel represents the bulb state.
    /// </summary>
    public class BulbService
    {
        private readonly int timeout;
        private readonly ILogger<BulbService> logger;
        private static readonly object _cacheLock = new object();
        private readonly UdpCommunicationService udpCommunicationService;

        /// <summary>
        /// Default timeout for bulb operations in milliseconds.
        /// </summary>
        public const int DefaultTimeout = 5000;

        /// <summary>
        /// Default port for WiZ bulbs.
        /// </summary>
        public const int DefaultPort = 38899;

        /// <summary>
        /// Cache of bulbs indexed by MAC address for quick lookup.
        /// </summary>
        public static Dictionary<MACAddress, BulbModel> BulbCache { get; } = 
            new Dictionary<MACAddress, BulbModel>();

        public BulbService(UdpCommunicationService udpCommunicationService, ILogger<BulbService> logger) : this(udpCommunicationService, DefaultTimeout, logger)
        {
        }

        /// <summary>
        /// Creates a BulbService with custom timeout.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds.</param>
        public BulbService(UdpCommunicationService udpCommunicationService, int timeout, ILogger<BulbService> logger)
        {
            this.udpCommunicationService = udpCommunicationService;
            this.timeout = timeout;
            this.logger = logger;
        }

        #region Discovery Operations

        /// <summary>
        /// Discovers bulbs on default network.
        /// </summary>
        /// <param name="timeout">Discovery timeout in milliseconds.</param>
        /// <param name="callback">Callback for each discovered bulb.</param>
        /// <returns>List of discovered bulbs.</returns>
        public async Task<List<BulbModel>> ScanForBulbsAsync(
            int timeout = 5000,
            Action<BulbModel> callback = null)
        {
            return await ScanForBulbsAsync(
                NetworkHelper.DefaultLocalIP,
                NetworkHelper.DefaultLocalMAC,
                ScanMode.GetSystemConfig,
                timeout,
                callback);
        }

        /// <summary>
        /// Discovers bulbs on specified network interface.
        /// </summary>
        /// <param name="localAddr">Local IP address.</param>
        /// <param name="macAddr">Local MAC address.</param>
        /// <param name="mode">Scan mode to use.</param>
        /// <param name="timeout">Discovery timeout in milliseconds.</param>
        /// <param name="callback">Callback for each discovered bulb.</param>
        /// <returns>List of discovered bulbs.</returns>
        public async Task<List<BulbModel>> ScanForBulbsAsync(
            IPAddress localAddr,
            MACAddress? macAddr,
            ScanMode mode = ScanMode.GetSystemConfig,
            int timeout = 5000,
            Action<BulbModel> callback = null)
        {
            var bulbs = new List<BulbModel>();
            var discoveredMacAddresses = new HashSet<string>();

            if (localAddr == null)
                localAddr = NetworkHelper.DefaultLocalIP;

            if (macAddr == null)
                macAddr = NetworkHelper.DefaultLocalMAC;

            logger.LogInformation("Starting bulb discovery on {LocalAddr} with mode {Mode}", localAddr, mode);

            var pilot = new BulbCommand();

            // Configure discovery command based on mode
            switch (mode)
            {
                case ScanMode.Registration:
                    pilot.Method = BulbMethod.Registration;
                    pilot.Params.PhoneMac = macAddr.ToString().Replace(":", "").ToLower();
                    pilot.Params.Register = false;
                    pilot.Params.PhoneIp = localAddr.ToString();
                    pilot.Params.Id = "12";
                    break;
                case ScanMode.GetPilot:
                    pilot.Method = BulbMethod.GetPilot;
                    break;
                default:
                    pilot.Method = BulbMethod.GetSystemConfig;
                    break;
            }

            var command = pilot.AssembleCommand();
            logger.LogInformation("Sending discovery command: {Command}", command);

            await udpCommunicationService.BroadcastCommandAsync(
                command,
                (response) =>
                {
                    try
                    {
                        var bulbCommand = new BulbCommand(response.Response);
                        if (bulbCommand.Result?.MACAddress == null) return;

                        var macAddress = bulbCommand.Result.MACAddress ?? MACAddress.None;
                        var macString = macAddress.ToString();

                        if (discoveredMacAddresses.Contains(macString)) return;

                        var bulbModel = CreateBulbFromResponse(response.SourceAddress, bulbCommand);

                        // Update cache
                        lock (_cacheLock)
                        {
                            BulbCache[macAddress] = bulbModel;
                        }

                        bulbs.Add(bulbModel);
                        discoveredMacAddresses.Add(macString);

                        callback?.Invoke(bulbModel);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing discovery response from {SourceAddress}", response.SourceAddress);
                    }
                },
                timeout);

            logger.LogInformation("Discovery completed. Found {Count} bulbs.", bulbs.Count);
            return bulbs;
        }

        /// <summary>
        /// Gets a bulb by its MAC address. Scans if not found in cache.
        /// </summary>
        public async Task<BulbModel> GetBulbByMacAsync(MACAddress macAddr, bool forceScan = false)
        {
            lock (_cacheLock)
            {
                if (!forceScan && BulbCache.ContainsKey(macAddr))
                    return BulbCache[macAddr];
            }

            await ScanForBulbsAsync(2000);

            lock (_cacheLock)
            {
                if (BulbCache.ContainsKey(macAddr))
                    return BulbCache[macAddr];
            }

            return null;
        }

        #endregion

        #region Bulb Operations

        /// <summary>
        /// Gets the current pilot (state) from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <returns>Updated bulb model with current state.</returns>
        public async Task<BulbModel> GetPilotAsync(BulbModel bulb)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            logger.LogInformation("Getting pilot state from bulb {MacAddress} at {IpAddress}", 
                bulb.MACAddress, bulb.IPAddress);

            var command = new BulbCommand
            {
                Method = BulbMethod.GetPilot
            };

            var response = await SendCommandAsync(bulb, command.AssembleCommand());
            
            if (!string.IsNullOrEmpty(response))
            {
                var bulbCommand = new BulbCommand(response);
                if (bulbCommand.Result != null)
                {
                    bulbCommand.Result.CopyTo(bulb.Settings);
                    bulb.UpdateLastSeen();
                }
            }

            return bulb;
        }

        /// <summary>
        /// Gets system configuration from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <returns>Updated bulb model with system configuration.</returns>
        public async Task<BulbModel> GetSystemConfigAsync(BulbModel bulb)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            logger.LogInformation("Getting system config from bulb {MacAddress} at {IpAddress}", 
                bulb.MACAddress, bulb.IPAddress);

            var command = new BulbCommand
            {
                Method = BulbMethod.GetSystemConfig
            };

            var response = await SendCommandAsync(bulb, command.AssembleCommand());
            
            if (!string.IsNullOrEmpty(response))
            {
                var bulbCommand = new BulbCommand(response);
                if (bulbCommand.Result != null)
                {
                    bulbCommand.Result.CopyTo(bulb.Settings);
                    bulb.UpdateLastSeen();
                }
            }

            return bulb;
        }

        /// <summary>
        /// Gets Model configuration from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <returns>Updated bulb model with model configuration.</returns>
        public async Task<BulbModel> GetModelConfigAsync(BulbModel bulb)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            logger.LogInformation("Getting model config from bulb {MacAddress} at {IpAddress}", 
                bulb.MACAddress, bulb.IPAddress);

            var command = new BulbCommand
            {
                Method = BulbMethod.GetModelConfig
            };

            var response = await SendCommandAsync(bulb, command.AssembleCommand());
            
            if (!string.IsNullOrEmpty(response))
            {
                var bulbCommand = new BulbCommand(response);
                if (bulbCommand.Result != null)
                {
                    bulbCommand.Result.CopyTo(bulb.Settings);
                    bulb.UpdateLastSeen();
                }
            }

            return bulb;
        }

        /// <summary>
        /// Turns a bulb on.
        /// </summary>
        /// <param name="bulb">The bulb to turn on.</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> TurnOnAsync(BulbModel bulb)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Turning on bulb {MacAddress}", bulb.MACAddress);

            bulb.Settings.State = true;
            return await SetPilotAsync(bulb);
        }

        /// <summary>
        /// Turns a bulb off.
        /// </summary>
        /// <param name="bulb">The bulb to turn off.</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> TurnOffAsync(BulbModel bulb)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Turning off bulb {MacAddress}", bulb.MACAddress);

            bulb.Settings.State = false;
            return await SetPilotAsync(bulb);
        }

        /// <summary>
        /// Sets the brightness of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set brightness for.</param>
        /// <param name="brightness">Brightness level (0-100).</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> SetBrightnessAsync(BulbModel bulb, int brightness)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting brightness to {Brightness}% for bulb {MacAddress}", brightness, bulb.MACAddress);

            bulb.Settings.Brightness = (byte)brightness;
            return await SetPilotAsync(bulb);
        }

        /// <summary>
        /// Sets the color of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set color for.</param>
        /// <param name="color">RGB color to set.</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> SetColorAsync(BulbModel bulb, System.Drawing.Color color)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting color to RGB({R},{G},{B}) for bulb {MacAddress}", 
                color.R, color.G, color.B, bulb.MACAddress);

            bulb.Settings.Color = color;
            return await SetPilotAsync(bulb);
        }

        /// <summary>
        /// Sets the temperature of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set temperature for.</param>
        /// <param name="temperature">Color temperature in Kelvin.</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> SetTemperatureAsync(BulbModel bulb, int temperature)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting temperature to {Temperature}K for bulb {MacAddress}", temperature, bulb.MACAddress);

            bulb.Settings.Scene = 0; // Set to manual mode
            bulb.Settings.Color = null; // Clear RGB values
            bulb.Settings.Temperature = temperature;
            return await SetPilotAsync(bulb);
        }

        /// <summary>
        /// Sets the scene of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set scene for.</param>
        /// <param name="scene">Scene to set.</param>
        /// <returns>The updated bulb model.</returns>
        public async Task<BulbModel> SetSceneAsync(BulbModel bulb, LightMode scene)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting scene to {Scene} for bulb {MacAddress}", scene.Name, bulb.MACAddress);

            bulb.Settings.Scene = (byte?)scene.Code;
            // LightModeInfo is read-only, will be updated internally by BulbParams
            return await SetPilotAsync(bulb);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Sends a command to a bulb and returns the response.
        /// </summary>
        /// <param name="bulb">The bulb to send command to.</param>
        /// <param name="command">JSON command to send.</param>
        /// <returns>Response from the bulb.</returns>
        private async Task<string> SendCommandAsync(BulbModel bulb, string command)
        {
            try
            {
                return await udpCommunicationService.SendCommandAsync(command, bulb.IPAddress, bulb.Port, timeout);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send command to {IpAddress}", bulb.IPAddress);
                return null;
            }
        }

        /// <summary>
        /// Sets the pilot (sends bulb settings) to a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set pilot for.</param>
        /// <returns>The updated bulb model.</returns>
        private async Task<BulbModel> SetPilotAsync(BulbModel bulb)
        {
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            var command = new BulbCommand
            {
                Method = BulbMethod.SetPilot,
                Params = bulb.Settings
            };

            var response = await SendCommandAsync(bulb, command.AssembleCommand());
            
            if (!string.IsNullOrEmpty(response))
            {
                var bulbCommand = new BulbCommand(response);
                if (bulbCommand.Result != null)
                {
                    bulbCommand.Result.CopyTo(bulb.Settings);
                    bulb.UpdateLastSeen();
                }
            }

            return bulb;
        }

        /// <summary>
        /// Creates a BulbModel from a discovery response.
        /// </summary>
        /// <param name="sourceAddress">Source IP address of the response.</param>
        /// <param name="bulbCommand">The bulb command containing bulb information.</param>
        /// <returns>A new BulbModel instance.</returns>
        private BulbModel CreateBulbFromResponse(IPAddress sourceAddress, BulbCommand bulbCommand)
        {
            var bulb = new BulbModel(sourceAddress)
            {
                MACAddress = bulbCommand.Result.MACAddress ?? MACAddress.None,
                Settings = bulbCommand.Result
            };

            // Update cache and existing bulb if found
            lock (_cacheLock)
            {
                var macAddr = bulbCommand.Result.MACAddress ?? MACAddress.None;
                if (BulbCache.ContainsKey(macAddr))
                {
                    var cachedBulb = BulbCache[macAddr];
                    bulbCommand.Result.CopyTo(cachedBulb.Settings);
                    cachedBulb.IPAddress = sourceAddress;
                    cachedBulb.Port = DefaultPort;
                    cachedBulb.UpdateLastSeen();
                    return cachedBulb;
                }
                else
                {
                    BulbCache[macAddr] = bulb;
                }
            }

            return bulb;
        }

        #endregion
    }
}