using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Operations;
using DrillingTelemetry.Processor.Processing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DrillingTelemetry.Processor.Messaging;

/// <summary>
/// Consumes telemetry readings from RabbitMQ and preserves ordering within
/// each telemetry stream while processing independent streams concurrently.
/// </summary>
internal sealed class RabbitMqTelemetryReadingConsumer
    : BackgroundService
{
    private const ushort OrderedConsumerDispatchConcurrency = 1;

    private const string DeadLetterExchangeArgument =
        "x-dead-letter-exchange";

    private const string DeadLetterRoutingKeyArgument =
        "x-dead-letter-routing-key";

    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly TimeProvider _timeProvider;
    private readonly TelemetryProcessingMetrics _metrics;
    private readonly IOperationalEventService _operationalEventService;
    private readonly ITelemetryReadingProcessor _telemetryReadingProcessor;
    private readonly ILogger<RabbitMqTelemetryReadingConsumer> _logger;

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
    /// Provides the UTC time used for invalid message events.
    /// </param>
    /// <param name="metrics">
    /// Records telemetry processing measurements.
    /// </param>
    /// <param name="telemetryReadingProcessor">
    /// Applies the processing policy to valid telemetry readings.
    /// </param>
    /// <param name="operationalEventService">
    /// Records invalid messages rejected by the consumer.
    /// </param>
    public RabbitMqTelemetryReadingConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTelemetryReadingConsumer> logger,
        TimeProvider timeProvider,
        TelemetryProcessingMetrics metrics,
        ITelemetryReadingProcessor telemetryReadingProcessor,
        IOperationalEventService operationalEventService)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(telemetryReadingProcessor);
        ArgumentNullException.ThrowIfNull(operationalEventService);

        _rabbitMqOptions = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _telemetryReadingProcessor = telemetryReadingProcessor;
        _operationalEventService = operationalEventService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName,
            ConsumerDispatchConcurrency =
                OrderedConsumerDispatchConcurrency
        };

        await using IConnection connection =
            await connectionFactory.CreateConnectionAsync(
                stoppingToken);

        await DeclareTopologyAsync(
            connection,
            stoppingToken);

        await RunPartitionedConsumerAsync(
            connection,
            stoppingToken);
    }

    /// <summary>
    /// Declares the telemetry queue and its dead-letter destination.
    /// </summary>
    /// <param name="connection">
    /// RabbitMQ connection used to create a temporary topology channel.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel topology declaration.
    /// </param>
    private async Task DeclareTopologyAsync(
        IConnection connection,
        CancellationToken cancellationToken)
    {
        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange:
                _rabbitMqOptions.TelemetryDeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _rabbitMqOptions.TelemetryDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: _rabbitMqOptions.TelemetryDeadLetterQueueName,
            exchange:
                _rabbitMqOptions.TelemetryDeadLetterExchangeName,
            routingKey:
                _rabbitMqOptions.TelemetryReadingsQueueName,
            arguments: null,
            cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object?>
        {
            [DeadLetterExchangeArgument] =
                _rabbitMqOptions.TelemetryDeadLetterExchangeName,
            [DeadLetterRoutingKeyArgument] =
                _rabbitMqOptions.TelemetryReadingsQueueName
        };

        await channel.QueueDeclareAsync(
            queue: _rabbitMqOptions.TelemetryReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArguments,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Receives ordered deliveries on one RabbitMQ channel and dispatches them
    /// to processing partitions selected by telemetry stream.
    /// </summary>
    /// <param name="connection">
    /// Shared RabbitMQ connection used to create the consumer channel.
    /// </param>
    /// <param name="stoppingToken">
    /// Token used to stop the consumer gracefully.
    /// </param>
    private async Task RunPartitionedConsumerAsync(
        IConnection connection,
        CancellationToken stoppingToken)
    {
        int partitionCount =
            _rabbitMqOptions.TelemetryProcessingPartitionCount;

        ushort totalPrefetchCount = checked((ushort)(
            _rabbitMqOptions.TelemetryPrefetchCount *
            partitionCount));

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: totalPrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        using var channelGate = new SemaphoreSlim(
            initialCount: 1,
            maxCount: 1);

        var partitioner = new TelemetryDeliveryPartitioner(
            partitionCount,
            _rabbitMqOptions.TelemetryPrefetchCount);

        Task partitionProcessingTask = partitioner.RunAsync(
            (delivery, cancellationToken) =>
                ProcessDeliveryAsync(
                    delivery,
                    channel,
                    channelGate,
                    cancellationToken),
            stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        async Task HandleDeliveryAsync(
            object sender,
            BasicDeliverEventArgs eventArgs)
        {
            await HandleReceivedAsync(
                eventArgs,
                partitioner,
                channel,
                channelGate);
        }

        consumer.ReceivedAsync += HandleDeliveryAsync;

        string consumerTag = await channel.BasicConsumeAsync(
            queue: _rabbitMqOptions.TelemetryReadingsQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Telemetry consumer is waiting for readings from " +
            "{QueueName} with {PartitionCount} processing " +
            "partitions and a total prefetch count of " +
            "{TotalPrefetchCount}",
            _rabbitMqOptions.TelemetryReadingsQueueName,
            partitionCount,
            totalPrefetchCount);

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
            consumer.ReceivedAsync -= HandleDeliveryAsync;

            await CancelConsumerAsync(
                channel,
                channelGate,
                consumerTag);

            await partitionProcessingTask;
        }
    }

    /// <summary>
    /// Deserialises a delivery and routes valid telemetry to its stream
    /// partition.
    /// </summary>
    /// <param name="eventArgs">
    /// RabbitMQ delivery information.
    /// </param>
    /// <param name="partitioner">
    /// Routes valid readings to sequential stream partitions.
    /// </param>
    /// <param name="channel">
    /// RabbitMQ channel that delivered the message.
    /// </param>
    /// <param name="channelGate">
    /// Serialises acknowledgement operations on the shared channel.
    /// </param>
    private async Task HandleReceivedAsync(
        BasicDeliverEventArgs eventArgs,
        TelemetryDeliveryPartitioner partitioner,
        IChannel channel,
        SemaphoreSlim channelGate)
    {
        try
        {
            TelemetryReading reading =
                DeserialiseReading(eventArgs);

            var delivery = new TelemetryDelivery(
                reading,
                eventArgs.DeliveryTag);

            await partitioner.EnqueueAsync(
                delivery,
                eventArgs.CancellationToken);
        }
        catch (JsonException exception)
        {
            _metrics.RecordInvalidMessage();

            _logger.LogWarning(
                exception,
                "Invalid telemetry message received");

            bool rejected = await RejectDeliveryAsync(
                channel,
                channelGate,
                eventArgs.DeliveryTag,
                requeue: false);

            if (rejected)
            {
                await RecordInvalidMessageAsync(
                    eventArgs.CancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (eventArgs.CancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Telemetry delivery {DeliveryTag} was interrupted " +
                "during consumer shutdown",
                eventArgs.DeliveryTag);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected failure dispatching telemetry delivery " +
                "{DeliveryTag}",
                eventArgs.DeliveryTag);

            await RejectDeliveryAsync(
                channel,
                channelGate,
                eventArgs.DeliveryTag,
                requeue: true);
        }
    }

    /// <summary>
    /// Processes one delivery and acknowledges its final outcome.
    /// </summary>
    /// <param name="delivery">Delivery assigned to a stream partition.</param>
    /// <param name="channel">
    /// RabbitMQ channel that owns the delivery tag.
    /// </param>
    /// <param name="channelGate">
    /// Serialises acknowledgement operations on the shared channel.
    /// </param>
    /// <param name="cancellationToken">
    /// Signals that delivery processing should stop.
    /// </param>
    private async Task ProcessDeliveryAsync(
        TelemetryDelivery delivery,
        IChannel channel,
        SemaphoreSlim channelGate,
        CancellationToken cancellationToken)
    {
        try
        {
            TelemetryProcessingResult processingResult =
                await _telemetryReadingProcessor.ProcessAsync(
                    delivery.Reading,
                    cancellationToken);

            if (processingResult == TelemetryProcessingResult.Conflict)
            {
                await RejectDeliveryAsync(
                    channel,
                    channelGate,
                    delivery.DeliveryTag,
                    requeue: false);

                return;
            }

            await AcknowledgeDeliveryAsync(
                channel,
                channelGate,
                delivery.DeliveryTag);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Telemetry delivery {DeliveryTag} was interrupted " +
                "during processor shutdown",
                delivery.DeliveryTag);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected failure processing telemetry delivery " +
                "{DeliveryTag}",
                delivery.DeliveryTag);

            await RejectDeliveryAsync(
                channel,
                channelGate,
                delivery.DeliveryTag,
                requeue: true);
        }
    }

    /// <summary>
    /// Deserialises and validates the identity of a telemetry reading.
    /// </summary>
    /// <param name="eventArgs">RabbitMQ delivery to deserialise.</param>
    /// <returns>The valid telemetry reading.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the payload is empty or has an invalid stream identity.
    /// </exception>
    private static TelemetryReading DeserialiseReading(
        BasicDeliverEventArgs eventArgs)
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

        if (reading.AcquisitionSessionId == Guid.Empty)
        {
            throw new JsonException(
                "The telemetry acquisition session is empty.");
        }

        if (reading.SequenceNumber <= 0)
        {
            throw new JsonException(
                "The telemetry sequence number must be greater " +
                "than zero.");
        }

        if (string.IsNullOrWhiteSpace(reading.WellId))
        {
            throw new JsonException(
                "The telemetry well identifier is empty.");
        }

        if (string.IsNullOrWhiteSpace(reading.WellboreId))
        {
            throw new JsonException(
                "The telemetry wellbore identifier is empty.");
        }

        if (!double.IsFinite(reading.MeasuredDepthMetres) ||
            reading.MeasuredDepthMetres < 0)
        {
            throw new JsonException(
                "The telemetry measured depth must be finite and not " +
                "negative.");
        }

        if (!Enum.IsDefined(reading.DrillingOperation))
        {
            throw new JsonException(
                "The telemetry drilling operation is invalid.");
        }

        if (!DrillingOperationValidation.IsValid(
                reading.DrillingOperation,
                reading.DepthChangeRateMetresPerHour))
        {
            throw new JsonException(
                "The telemetry drilling operation and depth-change rate " +
                "are inconsistent.");
        }

        return reading;
    }

    /// <summary>
    /// Acknowledges one delivery while serialising access to the channel.
    /// </summary>
    /// <param name="channel">Channel that owns the delivery.</param>
    /// <param name="channelGate">Serialises channel operations.</param>
    /// <param name="deliveryTag">Delivery identifier to acknowledge.</param>
    private static async Task AcknowledgeDeliveryAsync(
        IChannel channel,
        SemaphoreSlim channelGate,
        ulong deliveryTag)
    {
        await channelGate.WaitAsync(CancellationToken.None);

        try
        {
            await channel.BasicAckAsync(
                deliveryTag: deliveryTag,
                multiple: false,
                cancellationToken: CancellationToken.None);
        }
        finally
        {
            channelGate.Release();
        }
    }

    /// <summary>
    /// Negatively acknowledges a delivery and records channel failures.
    /// </summary>
    /// <param name="channel">
    /// Channel on which the delivery was received.
    /// </param>
    /// <param name="channelGate">
    /// Serialises acknowledgement operations on the shared channel.
    /// </param>
    /// <param name="deliveryTag">
    /// RabbitMQ delivery identifier.
    /// </param>
    /// <param name="requeue">
    /// Whether RabbitMQ should make the delivery available again.
    /// </param>
    private async Task<bool> RejectDeliveryAsync(
        IChannel channel,
        SemaphoreSlim channelGate,
        ulong deliveryTag,
        bool requeue)
    {
        await channelGate.WaitAsync(CancellationToken.None);

        try
        {
            await channel.BasicNackAsync(
                deliveryTag: deliveryTag,
                multiple: false,
                requeue: requeue,
                cancellationToken: CancellationToken.None);

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to reject telemetry delivery {DeliveryTag}",
                deliveryTag);

            return false;
        }
        finally
        {
            channelGate.Release();
        }
    }

    /// <summary>
    /// Cancels the RabbitMQ consumer during application shutdown.
    /// </summary>
    /// <param name="channel">Channel that owns the consumer.</param>
    /// <param name="channelGate">Serialises channel operations.</param>
    /// <param name="consumerTag">Consumer identifier to cancel.</param>
    private async Task CancelConsumerAsync(
        IChannel channel,
        SemaphoreSlim channelGate,
        string consumerTag)
    {
        await channelGate.WaitAsync(CancellationToken.None);

        try
        {
            await channel.BasicCancelAsync(
                consumerTag,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Telemetry consumer {ConsumerTag} was already stopped",
                consumerTag);
        }
        finally
        {
            channelGate.Release();
        }
    }

    /// <summary>
    /// Records that an invalid delivery was sent to the dead-letter queue.
    /// </summary>
    /// <param name="cancellationToken">
    /// Signals that event persistence should stop.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task RecordInvalidMessageAsync(
        CancellationToken cancellationToken)
    {
        var operationalEvent = new OperationalEvent(
            Guid.NewGuid(),
            OperationalEventType.InvalidMessage,
            OperationalEventSeverity.Warning,
            DeviceId: null,
            AcquisitionSessionId: null,
            SequenceNumber: null,
            PreviousSequenceNumber: null,
            GapSize: null,
            "An invalid telemetry message was rejected and sent " +
            "to the dead-letter queue.",
            _timeProvider.GetUtcNow());

        return _operationalEventService.RecordAsync(
            operationalEvent,
            cancellationToken);
    }
}
