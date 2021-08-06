using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using DevFlexer.RemoteConfigurationService.Hosting.Storages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlexer.RemoteConfigurationService.Hosting
{
    public static class RemoteConfigurationEndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapRemoteConfigurationService(this IEndpointRouteBuilder endpoints, string pattern = "/remote-configuration")
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            var conventionBuilders = new List<IEndpointConventionBuilder>();

            {
                var listConfigurationBuilder = endpoints.RegisterConfigurationListRoute(pattern);
                conventionBuilders.Add(listConfigurationBuilder);
            }

            {
                var fileConfigurationBuilder = endpoints.RegisterConfigurationFileRoute(pattern);
                conventionBuilders.Add(fileConfigurationBuilder);
            }

            return new CompositeEndpointConventionBuilder(conventionBuilders);
        }

        private static IEndpointConventionBuilder RegisterConfigurationListRoute(this IEndpointRouteBuilder endpointRouteBuilder, string pattern)
        {
            var storage = endpointRouteBuilder.ServiceProvider.GetService<IRemoteConfigurationStorage>();

            return endpointRouteBuilder.MapGet(pattern, async context =>
            {
                var files = await storage.ListPathsAsync();

                context.Response.OnStarting(async () =>
                {
                    await JsonSerializer.SerializeAsync(context.Response.Body, files);
                });

                context.Response.ContentType = "application/json; charset=UTF-8";
                await context.Response.Body.FlushAsync();
            });
        }

        private static IEndpointConventionBuilder RegisterConfigurationFileRoute(this IEndpointRouteBuilder endpointRouteBuilder, string pattern)
        {
            var storage = endpointRouteBuilder.ServiceProvider.GetService<IRemoteConfigurationStorage>();

            return endpointRouteBuilder.MapGet(pattern + "/{name}", async context =>
            {
                var name = context.GetRouteValue("name")?.ToString();
                name = WebUtility.UrlDecode(name);

                var bytes = await storage.GetConfigurationAsync(name);
                if (bytes == null)
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                var fileContent = Encoding.UTF8.GetString(bytes);
                await context.Response.WriteAsync(fileContent);
                await context.Response.Body.FlushAsync();
            });
        }

        private class CompositeEndpointConventionBuilder : IEndpointConventionBuilder
        {
            private readonly List<IEndpointConventionBuilder> _endpointConventionBuilders;

            public CompositeEndpointConventionBuilder(List<IEndpointConventionBuilder> endpointConventionBuilders)
            {
                _endpointConventionBuilders = endpointConventionBuilders;
            }

            public void Add(Action<EndpointBuilder> convention)
            {
                foreach (var endpointConventionBuilder in _endpointConventionBuilders)
                {
                    endpointConventionBuilder.Add(convention);
                }
            }
        }
    }
}
