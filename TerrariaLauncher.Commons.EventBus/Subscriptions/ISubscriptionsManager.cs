using System;
using System.Collections.Generic;
using System.Text;

namespace TerrariaLauncher.Commons.EventBus
{
    public class EventRemovedArgs : EventArgs
    {
        public string EventName { get; set; }
    }

    public interface ISubscriptionsManager
    {
        void AddSubscription<TEventData, TEventHandler>()
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>;
        void AddSubscription<TEventData, TEventHandler>(string eventName)
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>;
        void AddSubscription<TEventHandler>(string eventName)
            where TEventHandler : IJsonEventHandler;

        void RemoveSubscription<TEventData, TEventHandler>()
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>;
        void RemoveSubscription<TEventData, TEventHandler>(string eventName)
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>;
        void RemoveSubscription<TEventHandler>(string eventName)
            where TEventHandler : IJsonEventHandler;

        event EventHandler<EventRemovedArgs> OnEventRemoved;

        bool IsEmpty { get; }
        void Clear();

        IEnumerable<Subscription> GetSubscriptionsForEvent<TEventData>()
            where TEventData : IEventData;
        IEnumerable<Subscription> GetSubscriptionsForEvent(string eventName);

        bool HasSubscriptionForEvent<TEventData>() where TEventData : IEventData;
        bool HasSubscriptionForEvent(string eventName);
    }
}
