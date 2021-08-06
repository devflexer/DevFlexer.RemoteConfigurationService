using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using DevFlexer.RemoteConfigurationService.Client.Parsers;

namespace DevFlexer.RemoteConfigurationService.Client
{
    internal class RemoteConfigurationProvider : ConfigurationProvider, IDisposable
    {
        private readonly ILogger _logger;
        private readonly RemoteConfigurationSource _source;
        private readonly Lazy<HttpClient> _httpClient;
        private readonly IConfigurationFileParser _parser;
        private bool _isDisposed;

        private string Hash { get; set; }

        private HttpClient HttpClient => _httpClient.Value;

        public RemoteConfigurationProvider(RemoteConfigurationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));

            if (source.ConfigurationName == null)
            {
                throw new ArgumentNullException(nameof(source.ConfigurationName));
            }

            if (source.ConfigurationServiceUri == null)
            {
                throw new ArgumentNullException(nameof(source.ConfigurationServiceUri));
            }

            // Setup logging.
            Logger.LoggerFactory = source.LoggerFactory ?? new NullLoggerFactory();

            _logger = Logger.CreateLogger<RemoteConfigurationProvider>();

            _logger.LogInformation($"Initializing remote configuration source for configuration 'source.ConfigurationName'.");

            // Create http client.
            _httpClient = new Lazy<HttpClient>(CreateHttpClient);

            // Create configuration file parser.
            _parser = source.Parser;
            if (_parser == null)
            {
                var extension = Path.GetExtension(source.ConfigurationName).ToLower();

                _logger.LogInformation($"A file parser was not specified. Attempting to resolve parser from file extension '{extension}'.");

                _parser = CreateFileParserByExtension(extension);
            }

            _logger.LogInformation($"Using parser {_parser.GetType().Name}.");

            if (source.ReloadOnChange)
            {
                // ReloadOnChange를 지정했지만, Subscriber가 지정되지 않았으므로 원격지의 변경사항을 감지할 수 없음을 경고로 알려줌.
                if (source.CreateSubscriber == null)
                {
                    _logger.LogWarning("ReloadOnChange is enabled but a subscriber has not been configured.");
                    return;
                }

                var subscriber = source.CreateSubscriber();

                _logger.LogInformation($"Initializing remote configuration {subscriber.Name} subscriber for configuration '{source.ConfigurationName}'.");

                subscriber.Initialize();

                subscriber.Subscribe(source.ConfigurationName, message =>
                {
                    _logger.LogInformation($"Received remote configuration change subscription for configuration '{source.ConfigurationName}' with hash {message}. " +
                                           $"Current hash is {Hash}.");

                    if (message != null && message.Equals(Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Configuration '{source.ConfigurationName}' current hash {Hash} matches new hash. " +
                                               $"Configuration will not be updated.");

                        return;
                    }

                    Load();
                    OnReload();
                });
            }
        }

        private static IConfigurationFileParser CreateFileParserByExtension(string extension)
        {
            IConfigurationFileParser parser;
            switch (extension)
            {
                case ".ini":
                    parser = new IniConfigurationFileParser();
                    break;
                case ".xml":
                    parser = new XmlConfigurationFileParser();
                    break;
                case ".yml":
                case ".yaml":
                    parser = new YamlConfigurationFileParser();
                    break;
                default:
                    parser = new JsonConfigurationFileParser();
                    break;
            }

            return parser;
        }

        public override void Load() => LoadAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    var ex = task.Exception.Flatten();
                    _logger.LogError(ex, ex.Message);
                    throw ex;
                }
            }
        }).GetAwaiter().GetResult();

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_httpClient?.IsValueCreated == true)
            {
                _httpClient.Value.Dispose();
            }

            _isDisposed = true;
        }

        private HttpClient CreateHttpClient()
        {
            var handler = _source.HttpMessageHandler ?? new HttpClientHandler();
            var client = new HttpClient(handler, true)
            {
                BaseAddress = new Uri(_source.ConfigurationServiceUri),
                Timeout = _source.RequestTimeout
            };

            return client;
        }

        private async Task LoadAsync()
        {
            Data = await RequestConfigurationAsync().ConfigureAwait(false);
        }

        private async Task<IDictionary<string, string>> RequestConfigurationAsync()
        {
            var encodedConfigurationName = WebUtility.UrlEncode(_source.ConfigurationName);

            _logger.LogInformation($"Requesting remote configuration '{_source.ConfigurationName}' from '{HttpClient.BaseAddress}'...");

            try
            {
                using var response = await HttpClient.GetAsync(encodedConfigurationName).ConfigureAwait(false);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string statusMessage = $"Received response status code {(int)response.StatusCode}({response.StatusCode}) from endpoint for configuration '{_source.ConfigurationName}'.";
                    _logger.LogWarning(statusMessage);
                }
                else
                {
                    string statusMessage = $"Received from endpoint for configuration '{_source.ConfigurationName}'.";
                    _logger.LogInformation(statusMessage);
                }

                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    _logger.LogInformation($"Parsing remote configuration response stream ({stream.Length:N0} bytes) for configuration '{_source.ConfigurationName}'.");

                    Hash = ComputeHash(stream);
                    _logger.LogInformation($"Computed hash for Configuration '{_source.ConfigurationName}' is {Hash}.");

                    stream.Position = 0;
                    var data = _parser.Parse(stream);

                    _logger.LogInformation($"Configuration updated for '{_source.ConfigurationName}'.");

                    return data;
                }

                if (!_source.Optional)
                {
                    throw new Exception($"Error calling remote configuration endpoint: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception)
            {
                if (!_source.Optional)
                {
                    throw;
                }
            }

            return null;
        }

        //todo 이건 공용으로 빼줘도 좋을듯함.
        private string ComputeHash(Stream stream)
        {
            using (var hash = SHA1.Create())
            {
                var hashBytes = hash.ComputeHash(stream);

                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
