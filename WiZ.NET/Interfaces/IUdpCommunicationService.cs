using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace WiZ.NET.Interfaces
{
    /// <summary>
    /// Interface for UDP communication service.
    /// Provides abstraction for UDP-based bulb communication.
    /// </summary>
    public interface IUdpCommunicationService : IDisposable
    {
        /// <summary>
        /// Initializes the UDP service with the specified local address.
        /// </summary>
        /// <param name="localAddress">Local IP address to bind to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task InitializeAsync(IPAddress localAddress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a command to a bulb and waits for response.
        /// </summary>
        /// <param name="command">JSON command to send.</param>
        /// <param name="targetAddress">IP address of target bulb.</param>
        /// <param name="targetPort">Port of target bulb (default: 38899).</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Response from the bulb.</returns>
        Task<string> SendCommandAsync(
            string command, 
            IPAddress targetAddress, 
            int targetPort = 38899, 
            int timeout = 2000,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Broadcasts a command for bulb discovery.
        /// </summary>
        /// <param name="command">Command to broadcast.</param>
        /// <param name="callback">Callback for each response.</param>
        /// <param name="timeout">Discovery timeout.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task BroadcastCommandAsync(
            string command,
            Action<DiscoveryResponse> callback,
            int timeout = 5000,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Discovery response information.
    /// </summary>
    public class DiscoveryResponse
    {
        /// <summary>
        /// The response content.
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// The source IP address of the response.
        /// </summary>
        public IPAddress SourceAddress { get; set; }

        /// <summary>
        /// The timestamp when the response was received.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
