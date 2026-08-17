using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Realtime;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DrillingTelemetry.Processor.Messaging;

/// <summary>
/// Consumes and processes telemetry readings from RabbitMQ.
/// </summary>
internal sealed class RabbitMqTelemetryReadingConsumer
    : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ILogger<RabbitMqTelemetryReadingConsumer> _logger;

    private readonly ITelemetryReadingBroadcaster
        _telemetryReadingBroadcaster;

    /// <summary>
    /// Initialises a RabbitMQ telemetry reading consumer.
    /// </summary>
    /// <param name="options">
    /// Provides the RabbitMQ infrastructure configuration.
    /// </param>
    /// <param name="logger">
    /// Records consumer lifecycle and message processing information.
    /// </param>
    /// <param name="telemetryReadingBroadcaster"></param>
    public RabbitMqTelemetryReadingConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTelemetryReadingConsumer> logger,
        ITelemetryReadingBroadcaster telemetryReadingBroadcaster)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(telemetryReadingBroadcaster);

        _rabbitMqOptions = options.Value;
        _logger = logger;
        _telemetryReadingBroadcaster = telemetryReadingBroadcaster;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName
        };

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                stoppingToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _rabbitMqOptions.TelemetryReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += HandleReceivedAsync;

        string consumerTag = await channel.BasicConsumeAsync(
            queue: _rabbitMqOptions.TelemetryReadingsQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Waiting for telemetry readings from {QueueName}",
            _rabbitMqOptions.TelemetryReadingsQueueName);

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // The host requested a graceful shutdown.
        }
        finally
        {
            consumer.ReceivedAsync -= HandleReceivedAsync;

            await channel.BasicCancelAsync(
                consumerTag,
                cancellationToken: CancellationToken.None);
        }
    }

    /// <summary>
    /// Deserialises, processes and acknowledges a telemetry reading.
    /// </summary>
    /// <param name="sender">
    /// RabbitMQ consumer that received the message.
    /// </param>
    /// <param name="eventArgs">
    /// RabbitMQ delivery information.
    /// </param>
    private async Task HandleReceivedAsync(
        object sender,
        BasicDeliverEventArgs eventArgs)
    {
        var eventConsumer =
            (AsyncEventingBasicConsumer)sender;

        IChannel consumerChannel = eventConsumer.Channel;

        try
        {
            TelemetryReading? reading =
                JsonSerializer.Deserialize<TelemetryReading>(
                    eventArgs.Body.Span);

            if (reading is null)
            {
                throw new JsonException(
                    "The telemetry reading is empty.");
            }

            _logger.LogDebug(
                "Telemetry reading received from {DeviceId}: " +
                "{PressurePsi} psi, {TemperatureCelsius} °C at " +
                "{TimestampUtc:O}",
                reading.DeviceId,
                reading.PressurePsi,
                reading.TemperatureCelsius,
                reading.TimestampUtc);

            await _telemetryReadingBroadcaster.BroadcastAsync(
                reading,
                eventArgs.CancellationToken);

            await consumerChannel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid telemetry message received");

            await consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
        }
    }
}
