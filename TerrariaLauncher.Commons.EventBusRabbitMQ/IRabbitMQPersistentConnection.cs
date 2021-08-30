using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using TerrariaLauncher.Commons.EventBus;

namespace TerrariaLauncher.Commons.EventBusRabbitMQ
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        bool IsConnected { get; }
        bool TryConnect();
        IModel CreateModel();
    }
}
