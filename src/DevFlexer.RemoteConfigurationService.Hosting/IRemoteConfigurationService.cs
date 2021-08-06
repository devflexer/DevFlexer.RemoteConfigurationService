using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    /// <summary>
    /// 
    /// </summary>
    public interface IRemoteConfigurationService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        Task OnChangeAsync(IEnumerable<string> paths);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="paths"></param>
        /// <returns></returns>
        Task PublishChangesAsync(IEnumerable<string> paths);
    }
}
