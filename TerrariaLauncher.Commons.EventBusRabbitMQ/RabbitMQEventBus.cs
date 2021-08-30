using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TerrariaLauncher.Commons.EventBus;

namespace TerrariaLauncher.Commons.EventBusRabbitMQ
{
    public class EventBusRabbitMQ : IEventBus, IDisposable
    {
        readonly IRabbitMQPersistentConnection _persistentConnection;
        readonly ILogger<EventBusRabbitMQ> _logger;
        readonly ISubscriptionsManager _subsManager;
        readonly IServiceScopeFactory _scopeFactory;
        readonly int _retryCount;

        readonly string _exchangeName;
        string _queueName;

        IModel _consumerChannel;

        public EventBusRabbitMQ(
            IRabbitMQPersistentConnection persistentConnection,
            ILogger<EventBusRabbitMQ> logger,
            ISubscriptionsManager subsManager,
            IServiceScopeFactory scopeFactory,
            string exchangeName = "",
            string queueName = null,
            int retryCount = 5)
        {
            this._persistentConnection = persistentConnection;
            this._logger = logger;
            this._subsManager = subsManager;
            this._scopeFactory = scopeFactory;
            this._retryCount = retryCount;

            this._exchangeName = exchangeName;
            this._queueName = queueName;

            this._consumerChannel = this.CreateConsumerChannel();
            this.StartBasicConsume();
            this._subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        private IModel CreateConsumerChannel()
        {
            if (!this._persistentConnection.IsConnected)
            {
                this._persistentConnection.TryConnect();
            }

            this._logger.LogTrace("Creating RabbitMQ consumer channel.");

            var channel = this._persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: this._exchangeName, type: "direct");
            var queueDeclareOk = channel.QueueDeclare(queue: this._queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            this._queueName = queueDeclareOk.QueueName;

            channel.CallbackException += (sender, args) =>
            {
                this._logger.LogWarning(args.Exception, "Recreating RabbitMQ consumer channel.");
                this._consumerChannel.Dispose();
                this._consumerChannel = this.CreateConsumerChannel();
                this.StartBasicConsume();
            };

            return channel;
        }

        private void StartBasicConsume()
        {
            this._logger.LogTrace("Starting RabbitMQ basic consume.");

            if (this._consumerChannel != null)
            {
                var consumer = new AsyncEventingBasicConsumer(this._consumerChannel);
                consumer.Received += Consumer_Received;

                this._consumerChannel.BasicConsume(
                    queue: this._queueName,
                    autoAck: false,
                    consumer: consumer);
            }
            else
            {
                this._logger.LogError($"{nameof(this.StartBasicConsume)} can not call on {nameof(this._consumerChannel)} == null.");
            }
        }

        readonly UTF8Encoding utf8Encoding = new UTF8Encoding();
        private async Task Consumer_Received(object sender, BasicDeliverEventArgs args)
        {
            var eventName = args.RoutingKey;
            // Workaround for UTF8Encoding.GetString, in .NET Standard 2.0, without overload for ReadOnlyMemory.
            string message;
            var buffer = ArrayPool<byte>.Shared.Rent(args.Body.Length);
            try
            {
                args.Body.CopyTo(buffer);
                message = utf8Encoding.GetString(buffer, 0, args.Body.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            try
            {
                await this.ProcessEvent(eventName, message);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Error while processing message: \"{Message}\".", message);
            }

            this._consumerChannel.BasicAck(args.DeliveryTag, multiple: false);
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            this._logger.LogTrace("Processing RabbitMQ event: {EventName}.", eventName);

            if (this._subsManager.HasSubscriptionForEvent(eventName))
            {
                using (var scope = this._scopeFactory.CreateScope())
                {
                    var subscriptions = this._subsManager.GetSubscriptionsForEvent(eventName);
                    JsonDocument jsonDocument = null;
                    try
                    {
                        foreach (var subscription in subscriptions)
                        {
                            var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
                            if (handler == null) continue;

                            if (subscription.WithoutInstantiatingEventData)
                            {
                                if (jsonDocument is null)
                                {
                                    jsonDocument = JsonDocument.Parse(message);
                                }

                                await (handler as IJsonEventHandler)?.Handle(jsonDocument);
                            }
                            else
                            {
                                var eventData = JsonSerializer.Deserialize(message, subscription.EventDataType);
                                var concreteType = typeof(IEventHandler<>).MakeGenericType(subscription.EventDataType);
                                await (concreteType.GetMethod(nameof(IEventHandler<EventData>.Handle))
                                    .Invoke(handler, new object[] { eventData })
                                    as Task);
                            }
                        }
                    }
                    finally
                    {
                        jsonDocument?.Dispose();
                    }
                }
            }
            else
            {
                this._logger.LogWarning("No subscription for RabbitMQ event: {EventName}.", eventName);
            }
        }

        private void SubsManager_OnEventRemoved(object sender, EventRemovedArgs args)
        {
            this.DoRabbitMQQueueUnbinding(args.EventName);
        }

        public void Publish<TEventData>(TEventData eventData) where TEventData : IEventData
        {
            if (!this._persistentConnection.IsConnected)
            {
                this._persistentConnection.TryConnect();
            }

            var policy = RetryPolicy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(this._retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    this._logger.LogWarning(ex, "Could not publish event: {EventId} after {Timeout}s ({ExceptionMessage}).", eventData.Id, $"{time.TotalSeconds:n1}", ex.Message);
                });

            var eventName = eventData.GetType().Name;

            using (var channel = this._persistentConnection.CreateModel())
            {
                channel.ExchangeDeclare(exchange: this._exchangeName, type: "direct");

                var message = JsonSerializer.Serialize<TEventData>(eventData);
                var body = utf8Encoding.GetBytes(message);

                policy.Execute(() =>
                {
                    var properties = channel.CreateBasicProperties();
                    properties.DeliveryMode = 2;
                    channel.BasicPublish(
                        exchange: this._exchangeName,
                        routingKey: eventName,
                        mandatory: true,
                        basicProperties: properties,
                        body: body);
                });
            }
        }

        private void DoRabbitMQQueueBinding(string eventName)
        {
            if (this._subsManager.HasSubscriptionForEvent(eventName)) return;

            using (var channel = this._persistentConnection.CreateModel())
            {
                channel.QueueBind(
                    queue: this._queueName,
                    exchange: this._exchangeName,
                    routingKey: eventName);
            }
        }

        private void DoRabbitMQQueueUnbinding(string eventName)
        {
            if (this._subsManager.HasSubscriptionForEvent(eventName)) return;

            using (var channel = this._persistentConnection.CreateModel())
            {
                channel.QueueUnbind(
                    queue: this._queueName,
                    exchange: this._exchangeName,
                    routingKey: eventName);
            }
        }

        public void Subscribe<T, TH>()
            where T : EventData
            where TH : IEventHandler<T>
        {
            throw new NotImplementedException();
        }

        public void Unsubscribe<TEventData, TEventHandler>()
            where TEventData : EventData
            where TEventHandler : IEventHandler<TEventData>
        {
            this._subsManager.RemoveSubscription<TEventData, TEventHandler>();

        }

        public void SubscribeDynamic<TH>(string eventName) where TH : IJsonEventHandler
        {
            throw new NotImplementedException();
        }

        public void UnsubscribleDynamic<TH>(string eventName) where TH : IJsonEventHandler
        {
            throw new NotImplementedException();
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                this._consumerChannel?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
