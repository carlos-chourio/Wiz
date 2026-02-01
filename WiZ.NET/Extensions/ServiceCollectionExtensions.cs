using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WiZ.NET.Interfaces;
using WiZ.NET.Services;

namespace WiZ.NET.Extensions
{
    /// <summary>
    /// Extension methods for registering WiZ.NET services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds WiZ.NET services to the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="timeout">Optional custom timeout for bulb operations in milliseconds.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWiZNET(this IServiceCollection services, int? timeout = null)
        {
            // Register cache as singleton (shared state)
            services.AddSingleton<IBulbCache, BulbCache>();

            // Register UDP service as singleton (port binding is expensive)
            services.AddSingleton<IUdpCommunicationService, UdpCommunicationService>();

            // Register bulb service as singleton
            services.AddSingleton<IBulbService>(provider =>
            {
                var udpService = provider.GetRequiredService<IUdpCommunicationService>();
                var bulbCache = provider.GetRequiredService<IBulbCache>();
                var logger = provider.GetRequiredService<ILogger<BulbService>>();

                if (timeout.HasValue)
                {
                    return new BulbService(udpService, bulbCache, timeout.Value, logger);
                }

                return new BulbService(udpService, bulbCache, logger);
            });

            return services;
        }

        /// <summary>
        /// Adds WiZ.NET services with custom configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Configuration options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddWiZNET(
            this IServiceCollection services, 
            System.Action<WiZOptions> configureOptions)
        {
            var options = new WiZOptions();
            configureOptions?.Invoke(options);

            return services.AddWiZNET(options.Timeout);
        }
    }

    /// <summary>
    /// Configuration options for WiZ.NET services.
    /// </summary>
    public class WiZOptions
    {
        /// <summary>
        /// Timeout for bulb operations in milliseconds. Default is 5000ms.
        /// </summary>
        public int? Timeout { get; set; }
    }
}
