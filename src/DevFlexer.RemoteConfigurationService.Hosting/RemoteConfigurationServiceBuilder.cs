using System;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public class RemoteConfigurationServiceBuilder : IRemoteConfigurationServiceBuilder
    {
        public IServiceCollection Services { get; }

        public RemoteConfigurationServiceBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }
    }
}
