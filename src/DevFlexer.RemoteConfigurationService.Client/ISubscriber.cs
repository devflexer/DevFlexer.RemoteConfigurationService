using System;

namespace DevFlexer.RemoteConfigurationService.Client
{
    public interface ISubscriber
    {
        string Name { get; }

        void Initialize();

        void Subscribe(string topic, Action<string> handler);
    }
}
