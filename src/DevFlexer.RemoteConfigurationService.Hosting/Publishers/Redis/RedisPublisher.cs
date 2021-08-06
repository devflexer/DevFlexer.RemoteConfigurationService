﻿using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DevFlexer.RemoteConfigurationService.Hosting.Publishers.Redis
{
    public class RedisPublisher : IPublisher
    {
        private readonly ILogger<RedisPublisher> _logger;
        private readonly ConfigurationOptions _options;
        private static IConnectionMultiplexer _connection;

        public RedisPublisher(ILogger<RedisPublisher> logger, ConfigurationOptions configuration)
        {
            _logger = logger;
            _options = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task InitializeAsync()
        {
            using (var writer = new StringWriter())
            {
                _connection = ConnectionMultiplexer.Connect(_options, writer);

                _logger.LogDebug(writer.ToString());
            }

            _connection.ErrorMessage += (sender, args) => { _logger.LogError(args.Message); };
            _connection.ConnectionFailed += (sender, args) => { _logger.LogError(args.Exception, "Redis connection failed."); };
            _connection.ConnectionRestored += (sender, args) => { _logger.LogInformation("Redis connection restored."); };

            _logger.LogInformation("Redis publisher initialized.");

            return Task.CompletedTask;
        }

        public async Task PublishAsync(string channel, string message)
        {
            _logger.LogInformation("Publishing message to channel {channel}.", channel);

            var publisher = _connection.GetSubscriber();

            var clientCount = await publisher.PublishAsync(channel, message);

            _logger.LogInformation("Message to channel {channel} was received by {clientCount} clients.", channel, clientCount);
        }
    }
}
