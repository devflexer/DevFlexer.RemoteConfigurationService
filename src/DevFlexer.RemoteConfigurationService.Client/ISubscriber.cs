using System;

namespace DevFlexer.RemoteConfigurationService.Client
{
    //todo 이름을 어떤걸로 변경할까?
    public interface ISubscriber
    {
        string Name { get; }

        void Initialize();

        void Subscribe(string topic, Action<string> handler);
    }
}
