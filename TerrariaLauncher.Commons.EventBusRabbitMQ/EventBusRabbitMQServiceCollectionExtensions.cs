using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using TerrariaLauncher.Commons.EventBus;

namespace TerrariaLauncher.Commons.EventBusRabbitMQ
{
    public class EventBusRabbitMQConfiguration
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public int RetryCount { get; set; }
    }

    public static class EventBusRabbitMQServiceCollectionExtensions
    {
        public static IServiceCollection AddEventBusRabbitMQ(this IServiceCollection services, EventBusRabbitMQConfiguration configuration)
        {
            services.AddSingleton<ISubscriptionsManager, InMemorySubscriptionsManager>();
            services.AddSingleton<IEventBus>(serviceProvider =>
            {
                var connection = serviceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
                var logger = serviceProvider.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var subscriptionManager = serviceProvider.GetRequiredService<ISubscriptionsManager>();
                var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
                return new EventBusRabbitMQ(connection, logger, subscriptionManager, scopeFactory, configuration.ExchangeName, configuration.QueueName, configuration.RetryCount);
            });
            services.AddSingleton<IRabbitMQPersistentConnection>(serviceProvider =>
            {
                var rabbitMQConnectionFactory = new ConnectionFactory()
                {
                    HostName = configuration.Host,
                    Port = configuration.Port,
                    UserName = configuration.UserName,
                    Password = configuration.Password,
                    DispatchConsumersAsync = true
                };
                var logger = serviceProvider.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                return new DefaultRabbitMQPersistentConnection(rabbitMQConnectionFactory, logger, configuration.RetryCount);
            });
            return services;
        }
    }
}
