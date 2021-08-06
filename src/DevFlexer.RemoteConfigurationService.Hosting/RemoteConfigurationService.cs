using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DevFlexer.RemoteConfigurationService.Hosting.Storages;
using DevFlexer.RemoteConfigurationService.Hosting.Publishers;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public class RemoteConfigurationService : IRemoteConfigurationService
    {
        private readonly ILogger<RemoteConfigurationService> _logger;
        private readonly IRemoteConfigurationStorage _storage;
        private readonly IPublisher _publisher;

        public RemoteConfigurationService(ILogger<RemoteConfigurationService> logger, IRemoteConfigurationStorage storage, IPublisher publisher = null)
        {
            _logger = logger;
            _storage = storage;
            _publisher = publisher;

            if (_publisher == null)
            {
                _logger.LogInformation("A publisher has not been configured.");
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing {Name} configuration storage...", _storage.Name);

            // Initialize configuration storage.
            await _storage.InitializeAsync();

            // Initial optional propagator.
            // 최초에 한번은 무조건 보내줘야하므로 무조건 지정해야하는거 아닌가?
            if (_publisher != null)
            {
                _logger.LogInformation("Initializing publisher...");
                await _publisher.InitializeAsync();
            }

            // 저장소에서 파일 목록을 가져옴.
            var paths = await _storage.ListPathsAsync();

            // 최초한번은 보내줌.
            await PublishChangesAsync(paths);

            await _storage.WatchAsync(OnChangeAsync, cancellationToken);

            _logger.LogInformation("{Name} configuration watching for changes.", _storage.Name);
        }

        public async Task OnChangeAsync(IEnumerable<string> paths)
        {
            _logger.LogInformation("Changes were detected on the remote {Name} configuration storage.", _storage.Name);

            paths = paths.ToList();

            if (paths.Any())
            {
                await PublishChangesAsync(paths);
            }
        }

        public async Task PublishChangesAsync(IEnumerable<string> paths)
        {
            if (_publisher == null)
            {
                return;
            }

            _logger.LogInformation("Publishing changes...");

            foreach (var path in paths)
            {
                var hash = await _storage.GetHashAsync(path);
                //todo 동시에 여러개를 할 필요는 없을듯 싶은데..
                await _publisher.PublishAsync(path, hash);
            }
        }
    }
}
