using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Realtime;
using DrillingTelemetry.Processor.Sequencing;
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
    private readonly TimeProvider _timeProvider;
    private readonly TelemetryProcessingMetrics _metrics;
    private readonly TelemetrySequenceTracker _sequenceTracker;
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
    /// <param name="timeProvider">
    /// Provides the current UTC time used to calculate latency.
    /// </param>
    /// <param name="metrics">
    /// Records telemetry processing measurements.
    /// </param>
    /// <param name="sequenceTracker">
    /// Tracks the sequence observed for each telemetry device.
    /// </param>
    /// <param name="telemetryReadingBroadcaster">
    /// Broadcasts processed readings to connected clients.
    /// </param>
    public RabbitMqTelemetryReadingConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTelemetryReadingConsumer> logger,
        TimeProvider timeProvider,
        TelemetryProcessingMetrics metrics,
        TelemetrySequenceTracker sequenceTracker,
        ITelemetryReadingBroadcaster telemetryReadingBroadcaster)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(sequenceTracker);
        ArgumentNullException.ThrowIfNull(telemetryReadingBroadcaster);

        _rabbitMqOptions = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _sequenceTracker = sequenceTracker;
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

        Task[] consumerTasks = Enumerable
            .Range(
                start: 1,
                count: _rabbitMqOptions.TelemetryConsumerCount)
            .Select(consumerNumber =>
                RunConsumerAsync(
                    connection,
                    consumerNumber,
                    stoppingToken))
            .ToArray();

        await Task.WhenAll(consumerTasks);
    }

    /// <summary>
    /// Runs one sequential telemetry consumer on its own RabbitMQ channel.
    /// </summary>
    /// <param name="connection">
    /// Shared RabbitMQ connection used to create the consumer channel.
    /// </param>
    /// <param name="consumerNumber">
    /// Consumer number used to identify the worker in logs.
    /// </param>
    /// <param name="stoppingToken">
    /// Token used to stop the consumer gracefully.
    /// </param>
    private async Task RunConsumerAsync(
        IConnection connection,
        int consumerNumber,
        CancellationToken stoppingToken)
    {
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
            prefetchCount:
                _rabbitMqOptions.TelemetryPrefetchCount,
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
            "Telemetry consumer {ConsumerNumber} is waiting for " +
            "readings from {QueueName} " +
            "with a prefetch count of {PrefetchCount}",
            consumerNumber,
            _rabbitMqOptions.TelemetryReadingsQueueName,
            _rabbitMqOptions.TelemetryPrefetchCount);

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

            if (string.IsNullOrWhiteSpace(reading.DeviceId))
            {
                throw new JsonException(
                    "The telemetry device identifier is empty.");
            }

            if (reading.SequenceNumber <= 0)
            {
                throw new JsonException(
                    "The telemetry sequence number must be greater " +
                    "than zero.");
            }

            TelemetrySequenceObservation sequenceObservation =
                _sequenceTracker.Observe(
                    reading.DeviceId,
                    reading.SequenceNumber);

            RecordSequenceObservation(
                reading,
                sequenceObservation);

            _logger.LogDebug(
                "Telemetry reading {SequenceNumber} received " +
                "from {DeviceId}: " +
                "{PressurePsi} psi, {TemperatureCelsius} °C at " +
                "{TimestampUtc:O}",
                reading.SequenceNumber,
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

            _metrics.RecordReadingProcessed(
                _timeProvider.GetUtcNow() -
                reading.TimestampUtc);
        }
        catch (JsonException exception)
        {
            _metrics.RecordInvalidMessage();

            _logger.LogWarning(
                exception,
                "Invalid telemetry message received");

            await consumerChannel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
        }
    }

    /// <summary>
    /// Records sequence anomalies without discarding the telemetry reading.
    /// </summary>
    /// <param name="reading">Telemetry reading being processed.</param>
    /// <param name="observation">
    /// Result of comparing the reading with the previously observed sequence.
    /// </param>
    private void RecordSequenceObservation(
        TelemetryReading reading,
        TelemetrySequenceObservation observation)
    {
        switch (observation.Status)
        {
            case TelemetrySequenceStatus.Gap:
                _metrics.RecordSequenceGap(
                    observation.GapSize);

                _logger.LogWarning(
                    "Telemetry sequence gap detected for {DeviceId}: " +
                    "received {SequenceNumber} after " +
                    "{PreviousSequenceNumber}; " +
                    "{GapSize} sequence numbers were skipped",
                    reading.DeviceId,
                    reading.SequenceNumber,
                    observation.PreviousSequenceNumber,
                    observation.GapSize);
                break;

            case TelemetrySequenceStatus.Duplicate:
                _metrics.RecordDuplicateReading();

                _logger.LogWarning(
                    "Duplicate telemetry sequence {SequenceNumber} " +
                    "received from {DeviceId}",
                    reading.SequenceNumber,
                    reading.DeviceId);
                break;

            case TelemetrySequenceStatus.OutOfOrder:
                _metrics.RecordOutOfOrderReading();

                _logger.LogWarning(
                    "Out-of-order telemetry sequence {SequenceNumber} " +
                    "received from {DeviceId} after " +
                    "{PreviousSequenceNumber}",
                    reading.SequenceNumber,
                    reading.DeviceId,
                    observation.PreviousSequenceNumber);
                break;

            case TelemetrySequenceStatus.Baseline:
            case TelemetrySequenceStatus.InOrder:
            default:
                break;
        }
    }
}
