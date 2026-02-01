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
using WiZ.NET.Interfaces;

namespace WiZ.NET.Services
{
    /// <summary>
    /// Singleton UDP communication service for WiZ bulb communication.
    /// Provides thread-safe, managed UDP communication to prevent port binding conflicts.
    /// </summary>
    public class UdpCommunicationService : IUdpCommunicationService
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
        private CancellationTokenSource _listeningCts;

        // Simple retry configuration
        private const int DefaultRetryCount = 3;
        private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(800) };

        public UdpCommunicationService(ILogger<UdpCommunicationService> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task InitializeAsync(IPAddress localAddress = null, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
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
                _listeningCts = new CancellationTokenSource();
                _listeningTask = StartListeningAsync(_listeningCts.Token);
                _isInitialized = true;

                logger.LogInformation("UDP Communication Service initialized on {LocalAddress}:{Port}", 
                    localAddress, BulbService.DefaultPort);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<string> SendCommandAsync(
            string command, 
            IPAddress targetAddress, 
            int targetPort = BulbService.DefaultPort, 
            int timeout = 2000,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpCommunicationService));

            if (targetAddress == null)
                throw new ArgumentNullException(nameof(targetAddress));

            if (!_isInitialized)
                await InitializeAsync(cancellationToken: cancellationToken);

            // Simple retry loop
            Exception lastException = null;
            for (int attempt = 0; attempt <= DefaultRetryCount; attempt++)
            {
                try
                {
                    var result = await DoSendCommandAsync(command, targetAddress, targetPort, timeout, cancellationToken);
                    
                    // If we got a result (not null or empty), return it
                    if (!string.IsNullOrEmpty(result))
                    {
                        if (attempt > 0)
                        {
                            logger.LogInformation("Command succeeded after {Attempt} retry(s)", attempt);
                        }
                        return result;
                    }

                    // Empty result - will retry
                    logger.LogWarning("Empty response received, attempt {Attempt} of {MaxAttempts}", 
                        attempt + 1, DefaultRetryCount + 1);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is SocketException or TaskCanceledException or TimeoutException)
                {
                    lastException = ex;
                    logger.LogWarning(ex, "Send command failed on attempt {Attempt} of {MaxAttempts}", 
                        attempt + 1, DefaultRetryCount + 1);
                }

                // Wait before retry (if not the last attempt)
                if (attempt < DefaultRetryCount)
                {
                    var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                    logger.LogDebug("Waiting {Delay}ms before retry...", delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            // All retries exhausted
            logger.LogError(lastException, "Command failed after {RetryCount} retries", DefaultRetryCount);
            throw new InvalidOperationException($"Failed to send command after {DefaultRetryCount} retries", lastException);
        }

        private async Task<string> DoSendCommandAsync(
            string command, 
            IPAddress targetAddress, 
            int targetPort, 
            int timeout,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var requestId = Guid.NewGuid();
                var commandBytes = Encoding.UTF8.GetBytes(command);
                
                // Create pending request
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var pendingRequest = new PendingRequest
                {
                    RequestId = requestId,
                    TaskCompletionSource = tcs,
                    Timeout = timeout,
                    TargetAddress = targetAddress
                };
                
                _pendingRequests[requestId] = pendingRequest;

                using (logger.BeginScope(new Dictionary<string, object>
                {
                    ["RequestId"] = requestId,
                    ["TargetAddress"] = targetAddress,
                    ["TargetPort"] = targetPort
                }))
                {
                    logger.LogDebug("Sending command to {TargetAddress}:{TargetPort}", targetAddress, targetPort);

                    // Send the command
                    await _udpClient.SendAsync(commandBytes, commandBytes.Length, targetAddress.ToString(), targetPort);
                    
                    logger.LogOutput(command, _boundLocalAddress, targetAddress);
                }

                // Wait for response with timeout
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                
                linkedCts.Token.Register(() => 
                {
                    if (_pendingRequests.TryRemove(requestId, out _))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            tcs.TrySetCanceled(cancellationToken);
                        }
                        else
                        {
                            tcs.TrySetCanceled();
                        }
                    }
                });

                return await tcs.Task;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task BroadcastCommandAsync(
            string command,
            Action<DiscoveryResponse> callback,
            int timeout = 5000,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UdpCommunicationService));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (!_isInitialized)
                await InitializeAsync(cancellationToken: cancellationToken);

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var commandBytes = Encoding.UTF8.GetBytes(command);
                var broadcastAddress = IPAddress.Parse("255.255.255.255");
                
                // Register temporary discovery callback
                lock (_discoveryCallbacks)
                {
                    _discoveryCallbacks.Add(callback);
                }
                
                var endTime = DateTime.UtcNow.AddMilliseconds(timeout);
                
                using (logger.BeginScope(new Dictionary<string, object>
                {
                    ["Broadcast"] = true,
                    ["Timeout"] = timeout
                }))
                {
                    logger.LogInformation("Starting broadcast discovery for {Timeout}ms", timeout);

                    // Send initial broadcast
                    await _udpClient.SendAsync(commandBytes, commandBytes.Length, broadcastAddress.ToString(), BulbService.DefaultPort);
                    logger.LogOutput(command, _boundLocalAddress, broadcastAddress);
                }

                // Continue broadcasting periodically and collecting responses
                while (DateTime.UtcNow < endTime)
                {
                    await Task.Delay(500, cancellationToken); // Broadcast every 500ms
                    
                    if (DateTime.UtcNow < endTime)
                    {
                        await _udpClient.SendAsync(commandBytes, commandBytes.Length, broadcastAddress.ToString(), BulbService.DefaultPort);
                    }
                }

                // Remove the callback after timeout
                lock (_discoveryCallbacks)
                {
                    _discoveryCallbacks.Remove(callback);
                }

                logger.LogInformation("Broadcast discovery completed");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Listens for incoming UDP responses.
        /// </summary>
        private async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug("UDP listening task started");

            try
            {
                while (!cancellationToken.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        var result = await _udpClient.ReceiveAsync();
                        
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
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Timeout is expected, continue listening
                        logger.LogDebug("UDP receive timeout, continuing to listen");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in UDP listening loop");
                        // Continue listening despite errors
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("UDP listening task cancelled");
            }
            catch (ObjectDisposedException)
            {
                logger.LogDebug("UDP client disposed, listening task ending");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error in UDP listening task");
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
            List<Action<DiscoveryResponse>> callbacks;
            lock (_discoveryCallbacks)
            {
                callbacks = new List<Action<DiscoveryResponse>>(_discoveryCallbacks);
            }

            foreach (var callback in callbacks)
            {
                try
                {
                    callback?.Invoke(discoveryResponse);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error in discovery callback");
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
            
            logger.LogDebug("Disposing UDP Communication Service");

            // Cancel listening task
            _listeningCts?.Cancel();

            // Cancel all pending requests
            foreach (var request in _pendingRequests.Values)
            {
                request.TaskCompletionSource.TrySetCanceled();
            }
            _pendingRequests.Clear();

            // Close UDP client
            try
            {
                _udpClient?.Close();
                _udpClient?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error disposing UDP client");
            }
            _udpClient = null;

            _listeningCts?.Dispose();
            _semaphore?.Dispose();

            logger.LogInformation("UDP Communication Service disposed");
        }

        private class PendingRequest
        {
            public Guid RequestId { get; set; }
            public TaskCompletionSource<string> TaskCompletionSource { get; set; }
            public IPAddress TargetAddress { get; set; }
            public int Timeout { get; set; }
        }
    }
}
