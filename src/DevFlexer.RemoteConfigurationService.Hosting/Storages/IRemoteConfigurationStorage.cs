using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevFlexer.RemoteConfigurationService.Hosting.Storages
{
    public interface IRemoteConfigurationStorage
    {
        string Name { get; }

        Task WatchAsync(Func<IEnumerable<string>, Task> onChange, CancellationToken cancellationToken = default);

        Task InitializeAsync();

        Task<byte[]> GetConfigurationAsync(string name);

        Task<string> GetHashAsync(string name);

        Task<IEnumerable<string>> ListPathsAsync();
    }
}
