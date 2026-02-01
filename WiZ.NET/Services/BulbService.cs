using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WiZ.NET.Interfaces;
using WiZ.NET.Models;

namespace WiZ.NET.Services
{
    /// <summary>
    /// Service for handling WiZ bulb operations and communication.
    /// This service handles all bulb actions while BulbModel represents the bulb state.
    /// </summary>
    public class BulbService : IBulbService
    {
        private readonly int timeout;
        private readonly ILogger<BulbService> logger;
        private readonly IUdpCommunicationService udpCommunicationService;
        private readonly IBulbCache bulbCache;

        /// <summary>
        /// Default timeout for bulb operations in milliseconds.
        /// </summary>
        public const int DefaultTimeout = 5000;

        /// <summary>
        /// Default port for WiZ bulbs.
        /// </summary>
        public const int DefaultPort = 38899;

        /// <summary>
        /// Creates a BulbService with default timeout.
        /// </summary>
        public BulbService(
            IUdpCommunicationService udpCommunicationService, 
            IBulbCache bulbCache,
            ILogger<BulbService> logger) 
            : this(udpCommunicationService, bulbCache, DefaultTimeout, logger)
        {
        }

        /// <summary>
        /// Creates a BulbService with custom timeout.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds.</param>
        public BulbService(
            IUdpCommunicationService udpCommunicationService, 
            IBulbCache bulbCache,
            int timeout, 
            ILogger<BulbService> logger)
        {
            this.udpCommunicationService = udpCommunicationService ?? throw new ArgumentNullException(nameof(udpCommunicationService));
            this.bulbCache = bulbCache ?? throw new ArgumentNullException(nameof(bulbCache));
            this.timeout = timeout;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Discovery Operations

        /// <inheritdoc />
        public async Task<List<BulbModel>> ScanForBulbsAsync(
            int timeout = 5000,
            Action<BulbModel> callback = null,
            CancellationToken cancellationToken = default)
        {
            return await ScanForBulbsAsync(
                NetworkHelper.DefaultLocalIP,
                NetworkHelper.DefaultLocalMAC,
                ScanMode.GetSystemConfig,
                timeout,
                callback,
                cancellationToken);
        }

        /// <inheritdoc />
        public async Task<List<BulbModel>> ScanForBulbsAsync(
            IPAddress localAddr,
            MACAddress? macAddr,
            ScanMode mode = ScanMode.GetSystemConfig,
            int timeout = 5000,
            Action<BulbModel> callback = null,
            CancellationToken cancellationToken = default)
        {
            var bulbs = new List<BulbModel>();
            var discoveredMacAddresses = new HashSet<string>();

            if (localAddr == null)
                localAddr = NetworkHelper.DefaultLocalIP;

            if (macAddr == null)
                macAddr = NetworkHelper.DefaultLocalMAC;

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["LocalAddress"] = localAddr,
                ["ScanMode"] = mode,
                ["Timeout"] = timeout
            }))
            {
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
                logger.LogDebug("Sending discovery command: {Command}", command);

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
                            bulbCache.Set(macAddress, bulbModel);

                            bulbs.Add(bulbModel);
                            discoveredMacAddresses.Add(macString);

                            callback?.Invoke(bulbModel);

                            logger.LogDebug("Discovered bulb: {MacAddress} at {IpAddress}", 
                                macAddress, response.SourceAddress);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing discovery response from {SourceAddress}", 
                                response.SourceAddress);
                        }
                    },
                    timeout,
                    cancellationToken);

                logger.LogInformation("Discovery completed. Found {Count} bulbs.", bulbs.Count);
            }

            return bulbs;
        }

        /// <inheritdoc />
        public async Task<BulbModel> GetBulbByMacAsync(
            MACAddress macAddr, 
            bool forceScan = false,
            CancellationToken cancellationToken = default)
        {
            if (!forceScan && bulbCache.Contains(macAddr))
            {
                logger.LogDebug("Returning cached bulb: {MacAddress}", macAddr);
                return bulbCache.Get(macAddr);
            }

            logger.LogInformation("Bulb not in cache, scanning for {MacAddress}", macAddr);
            await ScanForBulbsAsync(2000, cancellationToken: cancellationToken);

            if (bulbCache.Contains(macAddr))
            {
                return bulbCache.Get(macAddr);
            }

            logger.LogWarning("Bulb not found: {MacAddress}", macAddr);
            return null;
        }

        #endregion

        #region Bulb Operations

        /// <inheritdoc />
        public async Task RefreshStateAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["MacAddress"] = bulb.MACAddress,
                ["IPAddress"] = bulb.IPAddress,
                ["Operation"] = "GetPilot"
            }))
            {
                logger.LogInformation("Getting pilot state from bulb {MacAddress} at {IpAddress}", 
                    bulb.MACAddress, bulb.IPAddress);

                var command = new BulbCommand
                {
                    Method = BulbMethod.GetPilot
                };

                var response = await SendCommandAsync(bulb, command.AssembleCommand(), cancellationToken);
                
                if (!string.IsNullOrEmpty(response))
                {
                    var bulbCommand = new BulbCommand(response);
                    if (bulbCommand.Result != null)
                    {
                        bulbCommand.Result.CopyTo(bulb.Settings);
                        bulb.UpdateLastSeen();
                    }
                }

            }
        }

        /// <inheritdoc />
        public async Task RefreshSystemConfigAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["MacAddress"] = bulb.MACAddress,
                ["IPAddress"] = bulb.IPAddress,
                ["Operation"] = "GetSystemConfig"
            }))
            {
                logger.LogInformation("Getting system config from bulb {MacAddress} at {IpAddress}", 
                    bulb.MACAddress, bulb.IPAddress);

                var command = new BulbCommand
                {
                    Method = BulbMethod.GetSystemConfig
                };

                var response = await SendCommandAsync(bulb, command.AssembleCommand(), cancellationToken);
                
                if (!string.IsNullOrEmpty(response))
                {
                    var bulbCommand = new BulbCommand(response);
                    if (bulbCommand.Result != null)
                    {
                        bulbCommand.Result.CopyTo(bulb.Settings);
                        bulb.UpdateLastSeen();
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task RefreshModelConfigAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));
                
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["MacAddress"] = bulb.MACAddress,
                ["IPAddress"] = bulb.IPAddress,
                ["Operation"] = "GetModelConfig"
            }))
            {
                logger.LogInformation("Getting model config from bulb {MacAddress} at {IpAddress}", 
                    bulb.MACAddress, bulb.IPAddress);

                var command = new BulbCommand
                {
                    Method = BulbMethod.GetModelConfig
                };

                var response = await SendCommandAsync(bulb, command.AssembleCommand(), cancellationToken);
                
                if (!string.IsNullOrEmpty(response))
                {
                    var bulbCommand = new BulbCommand(response);
                    if (bulbCommand.Result != null)
                    {
                        bulbCommand.Result.CopyTo(bulb.Settings);
                        bulb.UpdateLastSeen();
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<BulbModel> TurnOnAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Turning on bulb {MacAddress}", bulb.MACAddress);

            bulb.Settings.State = true;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BulbModel> TurnOffAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Turning off bulb {MacAddress}", bulb.MACAddress);

            bulb.Settings.State = false;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BulbModel> SetBrightnessAsync(
            BulbModel bulb, 
            int brightness,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting brightness to {Brightness}% for bulb {MacAddress}", 
                brightness, bulb.MACAddress);

            bulb.Settings.Brightness = (byte)brightness;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BulbModel> SetColorAsync(
            BulbModel bulb, 
            System.Drawing.Color color,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting color to RGB({R},{G},{B}) for bulb {MacAddress}", 
                color.R, color.G, color.B, bulb.MACAddress);

            bulb.Settings.Scene = null;
            bulb.Settings.Color = color;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BulbModel> SetTemperatureAsync(
            BulbModel bulb, 
            int temperature,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting temperature to {Temperature}K for bulb {MacAddress}", 
                temperature, bulb.MACAddress);

            bulb.Settings.Scene = 0; // Set to manual mode
            bulb.Settings.Color = null; // Clear RGB values
            bulb.Settings.Temperature = temperature;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<BulbModel> SetSceneAsync(
            BulbModel bulb, 
            LightMode scene,
            CancellationToken cancellationToken = default)
        {
            if (bulb == null)
                throw new ArgumentNullException(nameof(bulb));

            logger.LogInformation("Setting scene to {Scene} for bulb {MacAddress}", 
                scene.Name, bulb.MACAddress);

            bulb.Settings.Scene = (byte?)scene.Code;
            return await SetPilotAsync(bulb, cancellationToken);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Sends a command to a bulb and returns the response.
        /// </summary>
        private async Task<string> SendCommandAsync(
            BulbModel bulb, 
            string command,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await udpCommunicationService.SendCommandAsync(
                    command, bulb.IPAddress, bulb.Port, timeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
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
        private async Task<BulbModel> SetPilotAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default)
        {
            if (bulb.IPAddress == null)
                throw new InvalidOperationException("Bulb IP address is not set");

            var command = new BulbCommand
            {
                Method = BulbMethod.SetPilot,
                Params = bulb.Settings
            };

            var response = await SendCommandAsync(bulb, command.AssembleCommand(), cancellationToken);
            
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
        private BulbModel CreateBulbFromResponse(IPAddress sourceAddress, BulbCommand bulbCommand)
        {
            var macAddr = bulbCommand.Result.MACAddress ?? MACAddress.None;
            
            // Check if we already have this bulb cached
            var cachedBulb = bulbCache.Get(macAddr);
            if (cachedBulb != null)
            {
                bulbCommand.Result.CopyTo(cachedBulb.Settings);
                cachedBulb.IPAddress = sourceAddress;
                cachedBulb.Port = DefaultPort;
                cachedBulb.UpdateLastSeen();
                return cachedBulb;
            }

            // Create new bulb
            var bulb = new BulbModel(sourceAddress)
            {
                MACAddress = macAddr,
                Settings = bulbCommand.Result
            };

            bulbCache.Set(macAddr, bulb);
            return bulb;
        }

        #endregion
    }
}
