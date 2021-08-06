using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DevFlexer.RemoteConfigurationService.Hosting.Storages.FileSystem
{
    public class FileSystemStorage : IRemoteConfigurationStorage
    {
        private readonly ILogger<FileSystemStorage> _logger;
        private readonly FileSystemStorageOptions _storageOptions;
        private FileSystemWatcher _fileSystemWatcher;
        private Func<IEnumerable<string>, Task> _onChange;

        public string Name => "File System";

        public FileSystemStorage(ILogger<FileSystemStorage> logger, FileSystemStorageOptions storageOptions)
        {
            _logger = logger;
            _storageOptions = storageOptions;

            if (string.IsNullOrWhiteSpace(_storageOptions.Path))
            {
                throw new ArgumentNullException(nameof(_storageOptions.Path), $"{nameof(_storageOptions.Path)} cannot be NULL or empty.");
            }
        }

        public Task WatchAsync(Func<IEnumerable<string>, Task> onChange, CancellationToken cancellationToken = default)
        {
            _onChange = onChange;
            _fileSystemWatcher.EnableRaisingEvents = true;
            return Task.CompletedTask;
        }

        public Task InitializeAsync()
        {
            _logger.LogInformation("Initializing {Name} storage with options {Options}.", Name, new
            {
                _storageOptions.Path,
                _storageOptions.SearchPattern,
                _storageOptions.IncludeSubdirectories
            });

            if (_storageOptions.Username != null && _storageOptions.Password != null)
            {
                var credentials = new NetworkCredential(_storageOptions.Username, _storageOptions.Password, _storageOptions.Domain);
                var uri = new Uri(_storageOptions.Path);
                _ = new CredentialCache
                {
                    {new Uri($"{uri.Scheme}://{uri.Host}"), "Basic", credentials}
                };
            }

            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = _storageOptions.Path,
                Filter = _storageOptions.SearchPattern,
                IncludeSubdirectories = _storageOptions.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            _fileSystemWatcher.Created += HandleFileSystemWatcherChanged;
            _fileSystemWatcher.Changed += HandleFileSystemWatcherChanged;

            return Task.CompletedTask;
        }

        public async Task<byte[]> GetConfigurationAsync(string name)
        {
            string path = Path.Combine(_storageOptions.Path, name);

            if (!File.Exists(path))
            {
                _logger.LogInformation("File does not exist at {path}.", path);
                return null;
            }

            return await File.ReadAllBytesAsync(path);
        }

        public async Task<string> GetHashAsync(string name)
        {
            var bytes = await GetConfigurationAsync(name);
            return Hasher.CreateHash(bytes);
        }

        public async Task<IEnumerable<string>> ListPathsAsync()
        {
            _logger.LogInformation("Listing files at {Path}.", _storageOptions.Path);

            var searchOption = _storageOptions.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(_storageOptions.Path, _storageOptions.SearchPattern ?? "*", searchOption).ToList();
            files = files.Select(GetRelativePath).ToList();

            _logger.LogInformation("{Count} files found.", files.Count);

            return await Task.FromResult<IEnumerable<string>>(files);
        }

        private void HandleFileSystemWatcherChanged(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Detected file change at {FullPath}.", e.FullPath);

            var filename = GetRelativePath(e.FullPath);
            _onChange(new[] { filename });
        }

        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(_storageOptions.Path, fullPath);
        }
    }
}
