using System.Text.Json;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Configuration;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Processing;
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
    private const string DeadLetterExchangeArgument =
        "x-dead-letter-exchange";

    private const string DeadLetterRoutingKeyArgument =
        "x-dead-letter-routing-key";

    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly TelemetryProcessingMetrics _metrics;
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
    /// <param name="metrics">
    /// Records telemetry processing measurements.
    /// </param>
    /// <param name="telemetryReadingProcessor">
    /// Applies the processing policy to valid telemetry readings.
    /// </param>
    public RabbitMqTelemetryReadingConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTelemetryReadingConsumer> logger,
        TelemetryProcessingMetrics metrics,
        ITelemetryReadingProcessor telemetryReadingProcessor)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(telemetryReadingProcessor);

        _rabbitMqOptions = options.Value;
        _logger = logger;
        _metrics = metrics;
        _telemetryReadingProcessor = telemetryReadingProcessor;
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

        await DeclareTopologyAsync(
            connection,
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

            await _telemetryReadingProcessor.ProcessAsync(
                reading,
                eventArgs.CancellationToken);

            await consumerChannel.BasicAckAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false);

        }
        catch (JsonException exception)
        {
            _metrics.RecordInvalidMessage();

            _logger.LogWarning(
                exception,
                "Invalid telemetry message received");

            await RejectDeliveryAsync(
                consumerChannel,
                eventArgs.DeliveryTag);
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
                "Unexpected failure processing telemetry delivery " +
                "{DeliveryTag}",
                eventArgs.DeliveryTag);

            await RejectDeliveryAsync(
                consumerChannel,
                eventArgs.DeliveryTag);
        }
    }

    /// <summary>
    /// Rejects a delivery without requeueing it and records channel failures.
    /// </summary>
    /// <param name="channel">
    /// Channel on which the delivery was received.
    /// </param>
    /// <param name="deliveryTag">
    /// RabbitMQ delivery identifier.
    /// </param>
    private async Task RejectDeliveryAsync(
        IChannel channel,
        ulong deliveryTag)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag: deliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to reject telemetry delivery {DeliveryTag}",
                deliveryTag);
        }
    }

}
