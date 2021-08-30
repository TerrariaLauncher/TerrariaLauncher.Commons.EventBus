using System;
using System.Collections.Generic;
using System.Linq;

namespace TerrariaLauncher.Commons.EventBus
{
    public class InMemorySubscriptionsManager : ISubscriptionsManager
    {
        private readonly Dictionary<string, List<Subscription>> _subscriptions;

        public InMemorySubscriptionsManager()
        {
            this._subscriptions = new Dictionary<string, List<Subscription>>();
        }

        public event EventHandler<EventRemovedArgs> OnEventRemoved;

        public bool IsEmpty => !this._subscriptions.Keys.Any();
        public void Clear() => this._subscriptions.Clear();

        private string GetEventName<TEventData>() where TEventData : IEventData
        {
            return typeof(TEventData).Name;
        }

        private void DoAddSubscription(string eventName, Type eventDataType, Type handlerType)
        {
            if (!this.HasSubscriptionForEvent(eventName))
            {
                this._subscriptions.Add(eventName, new List<Subscription>());
            }

            if (this._subscriptions[eventName].Any(i => i.HandlerType == handlerType))
            {
                throw new ArgumentException(
                    $"Handler Type {handlerType.Name} already registered for '{eventName}'.",
                    nameof(handlerType));
            }

            if (eventDataType is null)
            {
                this._subscriptions[eventName].Add(Subscription.CreateSubscription(eventName, handlerType));
            }
            else
            {
                this._subscriptions[eventName].Add(Subscription.CreateSubscription(eventName, eventDataType, handlerType));
            }
        }

        private void DoRemoveHandler(string eventName, Subscription subscriptionToRemove)
        {
            if (subscriptionToRemove is null) return;

            this._subscriptions[eventName].Remove(subscriptionToRemove);
            if (!this._subscriptions[eventName].Any())
            {
                this._subscriptions.Remove(eventName);
                this.RaiseOnEventRemoved(eventName);
            }
        }

        private Subscription DoFindSubscriptionToRemove(string eventName, Type handlerType)
        {
            if (!this.HasSubscriptionForEvent(eventName))
            {
                return null;
            }

            return this._subscriptions[eventName].SingleOrDefault(s => s.HandlerType == handlerType);
        }

        private Subscription FindSubscriptionToRemove<TEvent, TEventHandler>()
            where TEvent : IEventData
            where TEventHandler : IEventHandler<TEvent>
        {
            var eventName = this.GetEventName<TEvent>();
            return this.DoFindSubscriptionToRemove(eventName, typeof(TEventHandler));
        }

        private Subscription FindDynamicSubscriptionToRemove<TEventHandler>(string eventName)
            where TEventHandler : IJsonEventHandler
        {
            return this.DoFindSubscriptionToRemove(eventName, typeof(TEventHandler));
        }

        private void RaiseOnEventRemoved(string eventName)
        {
            var handler = OnEventRemoved;
            handler?.Invoke(this, new EventRemovedArgs()
            {
                EventName = eventName
            });
        }

        public void AddSubscription<TEventData, TEventHandler>()
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>
        {
            var eventName = this.GetEventName<TEventData>();
            this.DoAddSubscription(eventName, typeof(TEventData), typeof(TEventHandler));
        }

        public void AddSubscription<TEventData, TEventHandler>(string eventName)
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>
        {
            this.DoAddSubscription(eventName, typeof(TEventData), typeof(TEventHandler));
        }

        public void AddSubscription<TEventHandler>(string eventName)
            where TEventHandler : IJsonEventHandler
        {
            this.DoAddSubscription(eventName, null, typeof(TEventHandler));
        }

        public void RemoveSubscription<TEvent, TEventHandler>()
            where TEvent : IEventData
            where TEventHandler : IEventHandler<TEvent>
        {
            var handlerToRemove = this.FindSubscriptionToRemove<TEvent, TEventHandler>();
            var eventName = this.GetEventName<TEvent>();
            this.DoRemoveHandler(eventName, handlerToRemove);
        }

        public void RemoveSubscription<TEvent, TEventHandler>(string eventName)
            where TEvent : IEventData
            where TEventHandler : IEventHandler<TEvent>
        {
            var handlerToRemove = this.FindSubscriptionToRemove<TEvent, TEventHandler>();
            this.DoRemoveHandler(eventName, handlerToRemove);
        }

        public void RemoveSubscription<TEventHandler>(string eventName) 
            where TEventHandler : IJsonEventHandler
        {
            var handlerToRemove = this.FindDynamicSubscriptionToRemove<TEventHandler>(eventName);
            this.DoRemoveHandler(eventName, handlerToRemove);
        }

        public IEnumerable<Subscription> GetSubscriptionsForEvent<TEvent>() where TEvent : IEventData
        {
            var key = this.GetEventName<TEvent>();
            return this.GetSubscriptionsForEvent(key);
        }

        public IEnumerable<Subscription> GetSubscriptionsForEvent(string eventName)
        {
            return this._subscriptions[eventName];
        }

        public bool HasSubscriptionForEvent<TEvent>() where TEvent : IEventData
        {
            var eventName = this.GetEventName<TEvent>();
            return this.HasSubscriptionForEvent(eventName);
        }

        public bool HasSubscriptionForEvent(string eventName)
        {
            return this._subscriptions.ContainsKey(eventName);
        }
    }
}
