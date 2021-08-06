using System;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using DevFlexer.RemoteConfigurationService.Hosting.Storages;
using DevFlexer.RemoteConfigurationService.Hosting.Storages.FileSystem;
using DevFlexer.RemoteConfigurationService.Hosting.Storages.Git;
using DevFlexer.RemoteConfigurationService.Hosting.Publishers;
using DevFlexer.RemoteConfigurationService.Hosting.Publishers.RabbitMq;
using DevFlexer.RemoteConfigurationService.Hosting.Publishers.Redis;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public static class RemoteConfigurationServiceBuilderExtensions
    {
        /// <summary>
        /// Adds services for configuration hosting to the specified <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// RemoteConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddRemoteConfigurationService(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddHostedService<HostedRemoteConfigurationService>();
            services.AddSingleton<IRemoteConfigurationService, RemoteConfigurationService>();

            return new RemoteConfigurationServiceBuilder(services);
        }

        /// <summary>
        /// Add Git as the configuration storage backend.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="configure">Configure git storage options.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddGitStorage(this IRemoteConfigurationServiceBuilder builder, Action<GitStorageOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new GitStorageOptions();
            configure(options);

            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<IRemoteConfigurationStorage, GitStorage>();

            return builder;
        }

        /// <summary>
        /// Add file system as the configuration storage backend.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="configure">Configure file system storage options.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddFileSystemStorage(this IRemoteConfigurationServiceBuilder builder, Action<FileSystemStorageOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new FileSystemStorageOptions();
            configure(options);

            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<IRemoteConfigurationStorage, FileSystemStorage>();

            return builder;
        }

        /// <summary>
        /// Adds a custom configuration storage backend.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="storage">The custom implementation of <see cref="IRemoteConfigurationStorage"/>.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddRemoteConfigStorage(this IRemoteConfigurationServiceBuilder builder, IRemoteConfigurationStorage storage)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            //옵션을 넣어줄수가 없구나?
            //제너릭으로 구현하면 되려나?

            builder.Services.AddSingleton(storage);

            return builder;
        }

        /// <summary>
        /// Adds RabbitMQ as the configuration publisher.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="configure">Configure options for the RabbitMQ publisher.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddRabbitMqPublisher(this IRemoteConfigurationServiceBuilder builder, Action<RabbitMqOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new RabbitMqOptions();
            configure(options);

            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<IPublisher, RabbitMqPublisher>();

            return builder;
        }

        /// <summary>
        /// Adds Redis as the configuration publisher.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="configure">Configure options for the Redis multiplexer.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddRedisPublisher(this IRemoteConfigurationServiceBuilder builder, Action<ConfigurationOptions> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var configurationOptions = new ConfigurationOptions();
            configure(configurationOptions);

            return AddRedisPublisher(builder, configurationOptions);
        }

        /// <summary>
        /// Adds Redis as the configuration publisher.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="configurationString">The string configuration for the Redis multiplexer.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddRedisPublisher(this IRemoteConfigurationServiceBuilder builder, string configurationString)
        {
            if (configurationString == null)
            {
                throw new ArgumentNullException(nameof(configurationString));
            }

            var configurationOptions = ConfigurationOptions.Parse(configurationString);

            return AddRedisPublisher(builder, configurationOptions);
        }

        private static IRemoteConfigurationServiceBuilder AddRedisPublisher(IRemoteConfigurationServiceBuilder builder, ConfigurationOptions configurationOptions)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configurationOptions == null)
            {
                throw new ArgumentNullException(nameof(configurationOptions));
            }

            builder.Services.AddSingleton(configurationOptions);
            builder.Services.AddSingleton<IPublisher, RedisPublisher>();

            return builder;
        }

        /// <summary>
        /// Adds a custom configuration publisher.
        /// </summary>
        /// <param name="builder">The <see cref="IRemoteConfigurationServiceBuilder"/> to add services to.</param>
        /// <param name="publisher">The custom implementation of <see cref="IPublisher"/>.</param>
        /// <returns>An <see cref="IRemoteConfigurationServiceBuilder"/> that can be used to further configure the 
        /// ConfigurationService services.</returns>
        public static IRemoteConfigurationServiceBuilder AddPublisher(this IRemoteConfigurationServiceBuilder builder, IPublisher publisher)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (publisher == null)
            {
                throw new ArgumentNullException(nameof(publisher));
            }

            //todo Options도 넣어주면 좋을듯함.

            builder.Services.AddSingleton(publisher);

            return builder;
        }
    }
}
