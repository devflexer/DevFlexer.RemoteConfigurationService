using Microsoft.Extensions.DependencyInjection;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    /// <summary>
    /// 
    /// </summary>
    public interface IRemoteConfigurationServiceBuilder
    {
        /// <summary>
        /// 
        /// </summary>
        IServiceCollection Services { get; }
    }
}
