using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace DevFlexer.RemoteConfigurationService.Samples.Client
{
    public class ConfigWriter
    {
        private readonly IOptionsMonitor<TestConfig> _testConfig;

        public ConfigWriter(IOptionsMonitor<TestConfig> testConfig)
        {
            _testConfig = testConfig;

            _testConfig.OnChange(config =>
            {
                Console.WriteLine($"configuration is changed: {config.Text}");
            });
        }

        public async Task Write(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //var config = _testConfig.CurrentValue;
                //if (!string.IsNullOrEmpty(config.Text))
                //{
                //    Console.WriteLine(config.Text);
                //}

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }
    }
}
