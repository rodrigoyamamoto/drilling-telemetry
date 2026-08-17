using System.Text.Json;
using DrillingTelemetry.Contracts.Commands;
using RabbitMQ.Client;

namespace DrillingTelemetry.Control.Api.Publishing;

/// <summary>
/// Publishes simulation settings commands through RabbitMQ.
/// </summary>
internal sealed class RabbitMqSimulationSettingsCommandPublisher
    : ISimulationSettingsCommandPublisher
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly BasicProperties _properties;
    private readonly SemaphoreSlim _publishLock = new(
        initialCount: 1,
        maxCount: 1);

    /// <summary>
    /// Initialises a RabbitMQ simulation settings command publisher.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to publish commands.
    /// </param>
    /// <param name="queueName">
    /// Destination settings command queue.
    /// </param>
    public RabbitMqSimulationSettingsCommandPublisher(
        IChannel channel,
        string queueName)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        _channel = channel;
        _queueName = queueName;

        _properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };
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
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
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
}