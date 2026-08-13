using System.Text;
using System.Text.Json;
using DrillingTelemetry.Contracts;
using RabbitMQ.Client;

namespace DrillingTelemetry.DeviceSimulator;

/// <summary>
/// Publishes telemetry readings to a RabbitMQ queue.
/// </summary>
internal sealed class RabbitMqTelemetryReadingPublisher
    : ITelemetryReadingPublisher
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly BasicProperties _properties;

    /// <summary>
    /// Initializes a RabbitMQ telemetry reading publisher.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to publish messages.
    /// </param>
    /// <param name="queueName">
    /// Name of the destination queue.
    /// </param>
    public RabbitMqTelemetryReadingPublisher(
        IChannel channel,
        string queueName)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        _channel = channel;
        _queueName = queueName;

        _properties = new BasicProperties
        {
            ContentType = "application/json", Persistent = true
        };
    }


    public async Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        string message = JsonSerializer.Serialize(reading);
        byte[] body = Encoding.UTF8.GetBytes(message);

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: true,
            basicProperties: _properties,
            body: body,
            cancellationToken: cancellationToken);

        Console.WriteLine($"Reading published to '{_queueName}':");
        Console.WriteLine(message);
    }


}
