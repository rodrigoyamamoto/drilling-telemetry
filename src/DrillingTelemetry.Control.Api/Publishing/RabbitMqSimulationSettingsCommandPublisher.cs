using System.Text.Json;
using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.Control.Api.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DrillingTelemetry.Control.Api.Publishing;

/// <summary>
/// Publishes simulation settings commands through RabbitMQ.
/// </summary>
internal sealed class RabbitMqSimulationSettingsCommandPublisher
    : ISimulationSettingsCommandPublisher,
      IHostedService,
      IAsyncDisposable
{
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly BasicProperties _properties;
    private readonly SemaphoreSlim _publishLock = new(
        initialCount: 1,
        maxCount: 1);

    private IConnection? _connection;
    private IChannel? _channel;
    private int _disposeState;

    /// <summary>
    /// Initialises a RabbitMQ simulation settings command publisher.
    /// </summary>
    /// <param name="options">
    /// Provides the RabbitMQ infrastructure configuration.
    /// </param>
    public RabbitMqSimulationSettingsCommandPublisher(
        IOptions<RabbitMqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rabbitMqOptions = options.Value;

        _properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };
    }

    /// <inheritdoc />
    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName
        };

        _connection =
            await connectionFactory.CreateConnectionAsync(
                cancellationToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        _channel =
            await _connection.CreateChannelAsync(
                options: channelOptions,
                cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange:
                _rabbitMqOptions.SimulationSettingsExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        UpdateSimulationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        byte[] body =
            JsonSerializer.SerializeToUtf8Bytes(command);

        await _publishLock.WaitAsync(cancellationToken);

        try
        {
            IChannel channel = _channel ??
                throw new InvalidOperationException(
                    "The RabbitMQ publisher has not started.");

            await channel.BasicPublishAsync(
                exchange:
                    _rabbitMqOptions.SimulationSettingsExchangeName,
                routingKey: string.Empty,
                mandatory: true,
                basicProperties: _properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _publishLock.WaitAsync();

        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }
}
