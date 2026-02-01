using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WiZ.NET.Models;

namespace WiZ.NET.Interfaces
{
    /// <summary>
    /// Interface for bulb service operations.
    /// Provides high-level operations for WiZ bulb control.
    /// </summary>
    public interface IBulbService
    {
        /// <summary>
        /// Discovers bulbs on default network.
        /// </summary>
        /// <param name="timeout">Discovery timeout in milliseconds.</param>
        /// <param name="callback">Callback for each discovered bulb.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered bulbs.</returns>
        Task<List<BulbModel>> ScanForBulbsAsync(
            int timeout = 5000,
            Action<BulbModel> callback = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers bulbs on specified network interface.
        /// </summary>
        /// <param name="localAddr">Local IP address.</param>
        /// <param name="macAddr">Local MAC address.</param>
        /// <param name="mode">Scan mode to use.</param>
        /// <param name="timeout">Discovery timeout in milliseconds.</param>
        /// <param name="callback">Callback for each discovered bulb.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered bulbs.</returns>
        Task<List<BulbModel>> ScanForBulbsAsync(
            IPAddress localAddr,
            MACAddress? macAddr,
            ScanMode mode = ScanMode.GetSystemConfig,
            int timeout = 5000,
            Action<BulbModel> callback = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a bulb by its MAC address. Scans if not found in cache.
        /// </summary>
        /// <param name="macAddr">The MAC address to search for.</param>
        /// <param name="forceScan">Force a new scan even if bulb is in cache.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The bulb model, or null if not found.</returns>
        Task<BulbModel> GetBulbByMacAsync(
            MACAddress macAddr, 
            bool forceScan = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refresh the current state from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated bulb model with current state.</returns>
        Task RefreshStateAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets system configuration from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated bulb model with system configuration.</returns>
        Task RefreshSystemConfigAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets Model configuration from a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated bulb model with model configuration.</returns>
        Task RefreshModelConfigAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Turns a bulb on.
        /// </summary>
        /// <param name="bulb">The bulb to turn on.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> TurnOnAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Turns a bulb off.
        /// </summary>
        /// <param name="bulb">The bulb to turn off.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> TurnOffAsync(
            BulbModel bulb,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the brightness of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set brightness for.</param>
        /// <param name="brightness">Brightness level (0-100).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> SetBrightnessAsync(
            BulbModel bulb, 
            int brightness,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the color of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set color for.</param>
        /// <param name="color">RGB color to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> SetColorAsync(
            BulbModel bulb, 
            System.Drawing.Color color,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the temperature of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set temperature for.</param>
        /// <param name="temperature">Color temperature in Kelvin.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> SetTemperatureAsync(
            BulbModel bulb, 
            int temperature,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the scene of a bulb.
        /// </summary>
        /// <param name="bulb">The bulb to set scene for.</param>
        /// <param name="scene">Scene to set.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The updated bulb model.</returns>
        Task<BulbModel> SetSceneAsync(
            BulbModel bulb, 
            LightMode scene,
            CancellationToken cancellationToken = default);
    }
}
