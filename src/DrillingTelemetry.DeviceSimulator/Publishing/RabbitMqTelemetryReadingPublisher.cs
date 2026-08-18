using Microsoft.Extensions.Logging;
using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator.Diagnostics;
using RabbitMQ.Client;

namespace DrillingTelemetry.DeviceSimulator.Publishing;

/// <summary>
/// Publishes telemetry readings to a RabbitMQ queue.
/// </summary>
internal sealed class RabbitMqTelemetryReadingPublisher
    : ITelemetryReadingPublisher
{
    private readonly IChannel _channel;
    private readonly string _queueName;
    private readonly BasicProperties _properties;
    private readonly TimeProvider _timeProvider;
    private readonly TelemetryPublishingMetrics _metrics;

    private readonly ILogger<RabbitMqTelemetryReadingPublisher> _logger;

    /// <summary>
    /// Initialises a RabbitMQ telemetry reading publisher.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to publish messages.
    /// </param>
    /// <param name="queueName">
    /// Name of the destination queue.
    /// </param>
    /// <param name="timeProvider">
    /// Provides monotonic timestamps used to measure publishing time.
    /// </param>
    /// <param name="metrics">
    /// Records telemetry publishing measurements.
    /// </param>
    /// <param name="logger">
    /// Records telemetry publishing information.
    /// </param>
    public RabbitMqTelemetryReadingPublisher(
        IChannel channel,
        string queueName,
        TimeProvider timeProvider,
        TelemetryPublishingMetrics metrics,
        ILogger<RabbitMqTelemetryReadingPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _queueName = queueName;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _logger = logger;

        _properties = new BasicProperties
        {
            ContentType = "application/json", Persistent = true
        };
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        byte[] body =
            JsonSerializer.SerializeToUtf8Bytes(reading);

        long startedTimestamp = _timeProvider.GetTimestamp();

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: true,
            basicProperties: _properties,
            body: body,
            cancellationToken: cancellationToken);

        _metrics.RecordReadingPublished(
            _timeProvider.GetElapsedTime(startedTimestamp));

        _logger.LogDebug(
            "Telemetry reading from {DeviceId} published to " +
            "{QueueName}: {PressurePsi} psi, " +
            "{TemperatureCelsius} °C at {TimestampUtc:O}",
            reading.DeviceId,
            _queueName,
            reading.PressurePsi,
            reading.TemperatureCelsius,
            reading.TimestampUtc);
    }
}
