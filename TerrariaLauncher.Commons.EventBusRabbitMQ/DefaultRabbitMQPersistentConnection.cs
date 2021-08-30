using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
using System.Net.Sockets;

namespace TerrariaLauncher.Commons.EventBusRabbitMQ
{
    public class DefaultRabbitMQPersistentConnection : IRabbitMQPersistentConnection
    {
        readonly IConnectionFactory _connectionFactory;
        readonly ILogger<DefaultRabbitMQPersistentConnection> _logger;
        readonly int _retryCount;
        IConnection _connection;

        readonly object sync_root = new object();

        public DefaultRabbitMQPersistentConnection(
            IConnectionFactory connectionFactory,
            ILogger<DefaultRabbitMQPersistentConnection> logger,
            int retryCount = 5)
        {
            this._connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._retryCount = retryCount;
        }

        public bool IsConnected => this._connection != null && this._connection.IsOpen && !this._disposed;

        public IModel CreateModel()
        {
            if (!this.IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action.");
            }

            return this._connection.CreateModel();
        }

        public bool TryConnect()
        {
            this._logger.LogInformation("RabbitMQ Client is trying to connect.");

            lock (sync_root)
            {
                var policy = RetryPolicy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(this._retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        this._logger.LogWarning(ex, "RabbitMQ Client cloud not connect after {TimeOut}s ({ExceptionMessage}).", $"{time.TotalSeconds:n1}", ex.Message);
                    });

                policy.Execute(() =>
                {
                    this._connection = this._connectionFactory.CreateConnection();
                });

                if (this.IsConnected)
                {
                    this._connection.ConnectionShutdown += OnConnectionShutdown;
                    this._connection.CallbackException += OnCallbackException;
                    this._connection.ConnectionBlocked += OnConnectionBlocked;

                    this._logger.LogInformation("RabbitMQ Client acquired a persistent connection to '{HostName}'.", this._connection.Endpoint.HostName);
                    return true;
                }
                else
                {
                    this._logger.LogCritical("RabbitMQ connections could not be created and opened.");
                    return false;
                }
            }
        }

        private void OnConnectionBlocked(object sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            if (this._disposed) return;

            this._logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");

            this.TryConnect();
        }

        private void OnCallbackException(object sender, RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
        {
            if (this._disposed) return;

            this._logger.LogWarning("A RabbitMQ connection throws exception. Trying to re-connect...");

            this.TryConnect();
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            if (this._disposed) return;

            this._logger.LogWarning("A RabbitMQ connection is on shutdown exception. Trying to re-connect...");

            this.TryConnect();
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    this._connection.Dispose();
                }
                catch (IOException ex)
                {
                    this._logger.LogCritical(ex, ex.Message);
                }
            }

            this._disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
