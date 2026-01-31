using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WiZ.NET.Helpers;

namespace WiZ.NET.Services
{
    /// <summary>
    /// Singleton UDP communication service for WiZ bulb communication.
    /// Provides thread-safe, managed UDP communication to prevent port binding conflicts.
    /// </summary>
    public class UdpCommunicationService : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<Guid, PendingRequest> _pendingRequests = new();
        private readonly List<Action<DiscoveryResponse>> _discoveryCallbacks = new();
        private readonly ILogger<UdpCommunicationService> logger;
        private UdpClient _udpClient;
        private bool _isInitialized = false;
        private bool _disposed = false;
        private IPAddress _boundLocalAddress;
        private Task _listeningTask;

        public UdpCommunicationService(ILogger<UdpCommunicationService> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Initializes the UDP service with the specified local address.
        /// </summary>
        /// <param name="localAddress">Local IP address to bind to.</param>
        public async Task InitializeAsync(IPAddress localAddress = null)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_isInitialized) return;

                localAddress ??= NetworkHelper.DefaultLocalIP;
                
                // Create and configure UDP client
                _udpClient = new UdpClient
                {
                    ExclusiveAddressUse = false,
                    Client = { 
                        ReceiveTimeout = 2000, // 2 second timeout
                        SendTimeout = 2000
                    }
                };

                // Bind to the default WiZ port (38899)
                var localEndPoint = new IPEndPoint(localAddress, BulbService.DefaultPort);
                _udpClient.Client.Bind(localEndPoint);
                _boundLocalAddress = localAddress;

                // Start listening for responses
                _listeningTask = StartListeningAsync();
                _isInitialized = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Sends a command to a bulb and waits for response.
        /// </summary>
        /// <param name="command">JSON command to send.</param>
        /// <param name="targetAddress">IP address of target bulb.</param>
        /// <param name="targetPort">Port of target bulb (default: 38899).</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>Response from the bulb.</returns>
        public async Task<string> SendCommandAsync(
            string command, 
            IPAddress targetAddress, 
            int targetPort = BulbService.DefaultPort, 
            int timeout = 2000)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpCommunicationService));

            if (!_isInitialized)
                await InitializeAsync();

            await _semaphore.WaitAsync();
            try
            {
                var requestId = Guid.NewGuid();
                var commandBytes = Encoding.UTF8.GetBytes(command);
                
                // Create pending request
                var tcs = new TaskCompletionSource<string>();
                var pendingRequest = new PendingRequest
                {
                    RequestId = requestId,
                    TaskCompletionSource = tcs,
                    Timeout = timeout,
                    TargetAddress = targetAddress
                };
                
                _pendingRequests[requestId] = pendingRequest;

                // Send the command
                _udpClient.Send(commandBytes, commandBytes.Length, targetAddress.ToString(), targetPort);
                
                logger.LogOutput(command, _boundLocalAddress, targetAddress);

                // Wait for response with timeout
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
                
                combinedCts.Token.Register(() => 
                {
                    if (_pendingRequests.TryRemove(requestId, out _))
                    {
                        tcs.TrySetCanceled();
                    }
                });

                return await tcs.Task;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Broadcasts a command for bulb discovery.
        /// </summary>
        /// <param name="command">Command to broadcast.</param>
        /// <param name="callback">Callback for each response.</param>
        /// <param name="timeout">Discovery timeout.</param>
        /// <returns>List of discovered bulbs.</returns>
        public async Task BroadcastCommandAsync(
            string command,
            Action<DiscoveryResponse> callback,
            int timeout = 5000)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpCommunicationService));

            if (!_isInitialized)
                await InitializeAsync();

            await _semaphore.WaitAsync();
            try
            {
                var commandBytes = Encoding.UTF8.GetBytes(command);
                var broadcastAddress = IPAddress.Parse("255.255.255.255");
                
                // Register temporary discovery callback
                _discoveryCallbacks.Add(callback);
                
                var endTime = DateTime.Now.AddMilliseconds(timeout);
                
                // Send initial broadcast
                _udpClient.Send(commandBytes, commandBytes.Length, broadcastAddress.ToString(), BulbService.DefaultPort);
                logger.LogOutput(command, _boundLocalAddress, broadcastAddress);

                // Continue broadcasting periodically and collecting responses
                while (DateTime.Now < endTime)
                {
                    await Task.Delay(500); // Broadcast every 500ms
                    
                    if (DateTime.Now < endTime)
                    {
                        _udpClient.Send(commandBytes, commandBytes.Length, broadcastAddress.ToString(), BulbService.DefaultPort);
                    }
                }

                // Remove the callback after timeout
                _discoveryCallbacks.Remove(callback);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Listens for incoming UDP responses.
        /// </summary>
        private async Task StartListeningAsync()
        {
            try
            {
                while (!_disposed)
                {
                    var receiveTask = _udpClient.ReceiveAsync();
                    
                    await receiveTask;
                    
                    if (receiveTask.IsCompleted)
                    {
                        var result = receiveTask.Result;
                        var response = Encoding.UTF8.GetString(result.Buffer).Trim('\x0');
                        
                        logger.LogInput(response, _boundLocalAddress, result.RemoteEndPoint.Address);

                        // Try to match with a pending request
                        var pendingRequest = FindPendingRequest(result.RemoteEndPoint.Address);
                        if (pendingRequest != null)
                        {
                            pendingRequest.TaskCompletionSource.TrySetResult(response);
                        }
                        else
                        {
                            // This might be a discovery response or unsolicited message
                            HandleDiscoveryResponse(response, result.RemoteEndPoint.Address);
                        }
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected during disposal
            }
            catch
            {
                // Log error but don't throw to prevent listening task from crashing
                // ConsoleHelper.LogError($"UDP listening error: {ex.Message}");
            }
        }

        /// <summary>
        /// Tries to find a pending request for the given remote address.
        /// </summary>
        private PendingRequest FindPendingRequest(IPAddress remoteAddress)
        {
            foreach (var kvp in _pendingRequests)
            {
                if (kvp.Value.TargetAddress.Equals(remoteAddress))
                {
                    _pendingRequests.TryRemove(kvp.Key, out var request);
                    return request;
                }
            }
            return null;
        }

        /// <summary>
        /// Handles discovery responses.
        /// </summary>
        private void HandleDiscoveryResponse(string response, IPAddress remoteAddress)
        {
            var discoveryResponse = new DiscoveryResponse
            {
                Response = response,
                SourceAddress = remoteAddress,
                Timestamp = DateTime.Now
            };

            // Notify all registered discovery callbacks
            foreach (var callback in _discoveryCallbacks.ToArray())
            {
                try
                {
                    callback?.Invoke(discoveryResponse);
                }
                catch
                {
                    // Don't let one bad callback break the others
                }
            }
        }

        /// <summary>
        /// Disposes the UDP communication service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            
            // Cancel all pending requests
            foreach (var request in _pendingRequests.Values)
            {
                request.TaskCompletionSource.TrySetCanceled();
            }
            _pendingRequests.Clear();

            // Close UDP client
            _listeningTask?.ContinueWith(_ => { });
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            _semaphore?.Dispose();
        }

        private class PendingRequest
        {
            public Guid RequestId { get; set; }
            public TaskCompletionSource<string> TaskCompletionSource { get; set; }
            public IPAddress TargetAddress { get; set; }
            public int Timeout { get; set; }
        }

        public class DiscoveryResponse
        {
            public string Response { get; set; }
            public IPAddress SourceAddress { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}