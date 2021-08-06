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

            services.AddRemoteConfigurationService()
                .AddGitStorage(c =>
                {
                    //todo �̰� configuration���� �������� �Ϸ��� ��� �ؾ��ұ�?
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

                //�ΰ��� endpoint�� �����ؾ���.
                endpoints.MapRemoteConfigurationService();
            });
        }
    }
}
