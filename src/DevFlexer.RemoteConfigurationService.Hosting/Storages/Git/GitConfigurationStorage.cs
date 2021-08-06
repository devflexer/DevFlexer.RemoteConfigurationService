using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;

namespace DevFlexer.RemoteConfigurationService.Hosting.Storages.Git
{
    public class GitStorage : IRemoteConfigurationStorage
    {
        private readonly ILogger<GitStorage> _logger;
        private readonly GitStorageOptions _storageOptions;
        private CredentialsHandler _credentialsHandler;

        public string Name => "Git";

        public GitStorage(ILogger<GitStorage> logger, GitStorageOptions storageOptions)
        {
            _logger = logger;
            _storageOptions = storageOptions;

            if (string.IsNullOrWhiteSpace(_storageOptions.LocalPath))
            {
                throw new ArgumentNullException(nameof(_storageOptions.LocalPath), $"{nameof(_storageOptions.LocalPath)} cannot be NULL or empty.");
            }

            if (string.IsNullOrWhiteSpace(_storageOptions.RepositoryUrl))
            {
                throw new ArgumentNullException(nameof(_storageOptions.RepositoryUrl), $"{nameof(_storageOptions.RepositoryUrl)} cannot be NULL or empty.");
            }
        }

        public async Task WatchAsync(Func<IEnumerable<string>, Task> onChange, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    List<string> files;

                    var task = Task.Run(ListChangedFilesAsync, cancellationToken);
                    // The git fetch operation can sometimes hang.  Force to complete after a minute.
                    if (task.Wait(TimeSpan.FromSeconds(60)))
                    {
                        files = task.Result.ToList();
                    }
                    else
                    {
                        throw new Exception("Attempting to list changed files timed out after 60 seconds.");
                    }

                    if (files.Count > 0)
                    {
                        await onChange(files);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An unhandled exception occurred while attempting to poll for changes");
                }

                var delayDate = DateTime.UtcNow.Add(_storageOptions.PollingInterval);

                _logger.LogInformation("Next polling period will begin in {PollingInterval:c} at {delayDate}.", _storageOptions.PollingInterval, delayDate);

                await Task.Delay(_storageOptions.PollingInterval, cancellationToken);
            }
        }

        public Task InitializeAsync()
        {
            _logger.LogInformation("Initializing {Name} storage with options {Options}.", Name, new
            {
                _storageOptions.RepositoryUrl,
                _storageOptions.LocalPath,
                _storageOptions.Branch,
                _storageOptions.PollingInterval,
                _storageOptions.SearchPattern
            });

            if (Directory.Exists(_storageOptions.LocalPath))
            {
                _logger.LogInformation("A local repository already exists at {LocalPath}.", _storageOptions.LocalPath);
                _logger.LogInformation("Deleting directory {LocalPath}.", _storageOptions.LocalPath);

                DeleteDirectory(_storageOptions.LocalPath);
            }

            if (!Directory.Exists(_storageOptions.LocalPath))
            {
                _logger.LogInformation("Creating directory {LocalPath}.", _storageOptions.LocalPath);

                Directory.CreateDirectory(_storageOptions.LocalPath);
            }

            if (_storageOptions.Username != null && _storageOptions.Password != null)
            {
                _credentialsHandler = (url, user, cred) => new UsernamePasswordCredentials
                {
                    Username = _storageOptions.Username,
                    Password = _storageOptions.Password
                };
            }

            var cloneOptions = new CloneOptions
            {
                CredentialsProvider = _credentialsHandler,
                BranchName = _storageOptions.Branch
            };

            _logger.LogInformation("Cloning git repository {RepositoryUrl} to {LocalPath}.", _storageOptions.RepositoryUrl, _storageOptions.LocalPath);

            var path = Repository.Clone(_storageOptions.RepositoryUrl, _storageOptions.LocalPath, cloneOptions);

            _logger.LogInformation("Repository cloned to {path}.", path);

            using var repository = new Repository(_storageOptions.LocalPath);
            var hash = repository.Head.Tip.Sha.Substring(0, 7); //github에서 확인해보니 7글자여야하는듯..

            _logger.LogInformation("Current HEAD is [{hash}] '{MessageShort}'.", hash, repository.Head.Tip.MessageShort);

            return Task.CompletedTask;
        }

        // 로컬에 받아진 파일을 가져옴.
        public async Task<byte[]> GetConfigurationAsync(string name)
        {
            string path = Path.Combine(_storageOptions.LocalPath, name);

            if (!File.Exists(path))
            {
                _logger.LogInformation("File does not exist at {path}.", path);
                return null;
            }

            return await File.ReadAllBytesAsync(path);
        }

        // 해당 파일의 Hash를 계산함.
        public async Task<string> GetHashAsync(string name)
        {
            var bytes = await GetConfigurationAsync(name);
            return Hasher.CreateHash(bytes);
        }

        public async Task<IEnumerable<string>> ListPathsAsync()
        {
            _logger.LogInformation("Listing files at {LocalPath}.", _storageOptions.LocalPath);

            IList<string> files = new List<string>();

            using (var repository = new Repository(_storageOptions.LocalPath))
            {
                _logger.LogInformation("Listing files in repository at {LocalPath}.", _storageOptions.LocalPath);

                foreach (var entry in repository.Index)
                {
                    files.Add(entry.Path);
                }
            }

            var localFiles = Directory.EnumerateFiles(_storageOptions.LocalPath, _storageOptions.SearchPattern ?? "*", SearchOption.AllDirectories).ToList();
            localFiles = localFiles.Select(GetRelativePath).ToList();

            files = localFiles.Intersect(files).ToList();

            _logger.LogInformation("{Count} files found.", files.Count);

            return await Task.FromResult<IEnumerable<string>>(files);
        }

        private async Task<IEnumerable<string>> ListChangedFilesAsync()
        {
            Fetch();

            IList<string> changedFiles = new List<string>();

            using (var repository = new Repository(_storageOptions.LocalPath))
            {
                _logger.LogInformation("Checking for remote changes on {RemoteName}.", repository.Head.TrackedBranch.RemoteName);

                foreach (TreeEntryChanges entry in repository.Diff.Compare<TreeChanges>(repository.Head.Tip.Tree, repository.Head.TrackedBranch.Tip.Tree))
                {
                    if (entry.Exists)
                    {
                        _logger.LogInformation("File {Path} changed.", entry.Path);
                        changedFiles.Add(entry.Path);
                    }
                    else
                    {
                        _logger.LogInformation("File {Path} no longer exists.", entry.Path);
                    }
                }
            }

            if (changedFiles.Count == 0)
            {
                _logger.LogInformation("No tree entry changes were detected.");

                return changedFiles;
            }

            UpdateLocal();

            var filteredFiles = await ListPathsAsync();
            changedFiles = filteredFiles.Intersect(changedFiles).ToList();

            _logger.LogInformation("{Count} files changed.", changedFiles.Count);

            return changedFiles;
        }

        private void UpdateLocal()
        {
            using var repository = new Repository(_storageOptions.LocalPath);
            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = _credentialsHandler
                }
            };

            var signature = new Signature(new Identity("Configuration Service", "Configuration Service"), DateTimeOffset.Now);

            _logger.LogInformation("Pulling changes to local repository.");

            var currentHash = repository.Head.Tip.Sha.Substring(0, 7); //github에 보니 hash가 7글자던데?

            _logger.LogInformation("Current HEAD is [{currentHash}] '{MessageShort}'.", currentHash, repository.Head.Tip.MessageShort);

            var result = Commands.Pull(repository, signature, options);

            _logger.LogInformation("Merge completed with status {Status}.", result.Status);

            var newHash = result.Commit.Sha.Substring(0, 6);

            _logger.LogInformation("New HEAD is [{newHash}] '{MessageShort}'.", newHash, result.Commit.MessageShort);
        }

        private static void DeleteDirectory(string path)
        {
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                DeleteDirectory(directory);
            }

            foreach (var fileName in Directory.EnumerateFiles(path))
            {
                var fileInfo = new FileInfo(fileName)
                {
                    Attributes = FileAttributes.Normal
                };

                fileInfo.Delete();
            }

            Directory.Delete(path);
        }

        private void Fetch()
        {
            using var repository = new Repository(_storageOptions.LocalPath);
            FetchOptions options = new FetchOptions
            {
                CredentialsProvider = _credentialsHandler
            };

            foreach (var remote in repository.Network.Remotes)
            {
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                _logger.LogInformation("Fetching from remote {Name} at {Url}.", remote.Name, remote.Url);

                Commands.Fetch(repository, remote.Name, refSpecs, options, string.Empty);
            }
        }

        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(_storageOptions.LocalPath, fullPath);
        }
    }
}
