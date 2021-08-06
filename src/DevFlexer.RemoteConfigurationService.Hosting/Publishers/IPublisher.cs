using System.Threading.Tasks;

namespace DevFlexer.RemoteConfigurationService.Hosting.Publishers
{
    public interface IPublisher
    {
        Task InitializeAsync();

        Task PublishAsync(string topic, string message);
    }
}
