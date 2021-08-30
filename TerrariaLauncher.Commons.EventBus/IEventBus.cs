using System;

namespace TerrariaLauncher.Commons.EventBus
{
    public interface IEventBus
    {
        void Publish<TEventData>(TEventData eventData) where TEventData : IEventData;

        void Subscribe<T, TH>()
            where T : EventData
            where TH : IEventHandler<T>;

        void Unsubscribe<T, TH>()
            where T : EventData
            where TH : IEventHandler<T>;

        void SubscribeDynamic<TH>(string eventName)
            where TH : IJsonEventHandler;

        void UnsubscribleDynamic<TH>(string eventName)
            where TH : IJsonEventHandler;
    }
}
