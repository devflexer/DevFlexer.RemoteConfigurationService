using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DevFlexer.RemoteConfigurationService.Hosting;

namespace ConfigurationService.Samples.Host
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            //기본 appsettings에서 값을 가져와서 처리할 수 있으면 좋을듯한데..

            services.AddRemoteConfigurationService()
                .AddGitStorage(c =>
                {
                    c.RepositoryUrl = "https://github.com/devflexer/remote-configuration-storage-test.git";
                    c.LocalPath = "C:/LocalRepository";
                    c.Branch = "main";
                    c.Username = "devflexer@gmail.com";
                    c.Password = "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";
                    c.PollingInterval = System.TimeSpan.FromSeconds(30);
                })
                .AddRedisPublisher("localhost:6379");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // github action등을 이용해서 지정한 엔트포인트를 호출하면 감지하게할수도 있지 않을까?
                // 헌데 이건 서버에서만 감지되는거지 클라에는 감지안됨임.
                // 즉, github 폴링만 억제할수 있다는 얘기임.

                endpoints.MapRemoteConfigurationService();
            });
        }
    }
}
