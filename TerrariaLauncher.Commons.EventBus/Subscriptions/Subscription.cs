using System;
using System.Text.Json;

namespace TerrariaLauncher.Commons.EventBus
{
    public class Subscription
    {
        public string EventName { get; }
        public Type EventDataType { get; }
        public Type HandlerType { get; }
        public bool WithoutInstantiatingEventData { get => this.EventDataType is null; }

        private Subscription(string eventName, Type eventDataType , Type handlerType)
        {
            this.EventName = eventName;
            this.EventDataType = eventDataType;
            this.HandlerType = handlerType;
        }

        public static Subscription CreateSubscription<TEventData, TEventHandler>(string eventName) 
            where TEventData: IEventData 
            where TEventHandler: IEventHandler<TEventData>
        {
            return CreateSubscription(eventName, typeof(TEventData), typeof(TEventHandler));
        }

        public static Subscription CreateSubscription<TEventData, TEventHandler>()
            where TEventData : IEventData
            where TEventHandler : IEventHandler<TEventData>
        {
            return CreateSubscription(typeof(TEventData).Name, typeof(TEventData), typeof(TEventHandler));
        }

        public static Subscription CreateSubscription(string eventName, Type eventDataType, Type eventHandlerType)
        {
            return new Subscription(eventName, eventDataType, eventHandlerType);
        }

        public static Subscription CreateSubscription(Type eventDataType, Type eventHandlerType)
        {
            return new Subscription(eventDataType.Name, eventDataType, eventHandlerType);
        }

        public static Subscription CreateSubscription<TEventHandler>(string eventName)
            where TEventHandler : IJsonEventHandler
        {
            return new Subscription(eventName, null, typeof(TEventHandler));
        }

        public static Subscription CreateSubscription(string eventName, Type eventHandlerType)
        {
            return new Subscription(eventName, null, eventHandlerType);
        }
    }
}
