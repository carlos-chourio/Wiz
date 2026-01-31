using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WiZ.NET.Services;

namespace WiZ.NET.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWiZNET(IServiceCollection services, int? timeout = null)
        {
            services.AddSingleton<UdpCommunicationService>();

            return services.AddSingleton(provider=>
            {
                var commService = provider.GetService<UdpCommunicationService>();
                var logger = provider.GetService<ILogger<BulbService>>();

                if (timeout.HasValue)
                {
                    return new BulbService(commService, timeout.Value, logger);
                }

                return new BulbService(commService, logger);
            });            
        }
    }
    
}