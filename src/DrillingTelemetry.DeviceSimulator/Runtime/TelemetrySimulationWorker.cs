using DrillingTelemetry.DeviceSimulator.Configuration;
using DrillingTelemetry.DeviceSimulator.Diagnostics;
using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Publishing;
using DrillingTelemetry.DeviceSimulator.RuntimeSettings;
using DrillingTelemetry.DeviceSimulator.Simulation;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DrillingTelemetry.DeviceSimulator.Runtime;

/// <summary>
/// Runs the telemetry simulation for the lifetime of the application.
/// </summary>
internal sealed class TelemetrySimulationWorker : BackgroundService
{
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly ITelemetryReadingGenerator _readingGenerator;
    private readonly SimulationSettingsState _settingsState;
    private readonly SimulationSettingsCommandApplier _settingsCommandApplier;
    private readonly TimeProvider _timeProvider;
    private readonly TelemetryPublishingMetrics _publishingMetrics;
    private readonly ILogger<TelemetrySimulationWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initialises the telemetry simulation worker.
    /// </summary>
    /// <param name="rabbitMqOptions">
    /// Provides the RabbitMQ infrastructure configuration.
    /// </param>
    /// <param name="readingGenerator">
    /// Generates telemetry readings.
    /// </param>
    /// <param name="settingsState">
    /// Contains the current runtime simulation settings.
    /// </param>
    /// <param name="settingsCommandApplier">
    /// Applies settings received while the simulation is running.
    /// </param>
    /// <param name="timeProvider">
    /// Provides time used by the simulation.
    /// </param>
    /// <param name="publishingMetrics">
    /// Records telemetry publishing measurements.
    /// </param>
    /// <param name="loggerFactory">
    /// Creates loggers for RabbitMQ components owned by the worker.
    /// </param>
    /// <param name="logger">
    /// Records simulation lifecycle information.
    /// </param>
    public TelemetrySimulationWorker(
        IOptions<RabbitMqOptions> rabbitMqOptions,
        ITelemetryReadingGenerator readingGenerator,
        SimulationSettingsState settingsState,
        SimulationSettingsCommandApplier settingsCommandApplier,
        TimeProvider timeProvider,
        TelemetryPublishingMetrics publishingMetrics,
        ILoggerFactory loggerFactory,
        ILogger<TelemetrySimulationWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(rabbitMqOptions);
        ArgumentNullException.ThrowIfNull(readingGenerator);
        ArgumentNullException.ThrowIfNull(settingsState);
        ArgumentNullException.ThrowIfNull(settingsCommandApplier);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(publishingMetrics);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _rabbitMqOptions = rabbitMqOptions.Value;
        _readingGenerator = readingGenerator;
        _settingsState = settingsState;
        _settingsCommandApplier = settingsCommandApplier;
        _timeProvider = timeProvider;
        _publishingMetrics = publishingMetrics;
        _loggerFactory = loggerFactory;
        _logger = logger;
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

        await using IChannel telemetryChannel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await using IChannel settingsChannel =
            await connection.CreateChannelAsync(
                cancellationToken: stoppingToken);

        await telemetryChannel.QueueDeclareAsync(
            queue: _rabbitMqOptions.TelemetryReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        var readingPublisher =
            new RabbitMqTelemetryReadingPublisher(
                telemetryChannel,
                _rabbitMqOptions.TelemetryReadingsQueueName,
                _timeProvider,
                _publishingMetrics,
                _loggerFactory.CreateLogger<
                    RabbitMqTelemetryReadingPublisher>());

        var settingsConsumer =
            new RabbitMqSimulationSettingsConsumer(
                settingsChannel,
                _rabbitMqOptions.SimulationSettingsQueueName,
                _settingsCommandApplier,
                _loggerFactory.CreateLogger<
                    RabbitMqSimulationSettingsConsumer>());

        var simulation = new TelemetrySimulation(
            _readingGenerator,
            readingPublisher,
            _timeProvider,
            _settingsState);

        string settingsConsumerTag =
            await settingsConsumer.StartAsync(stoppingToken);

        _logger.LogInformation(
            "Telemetry simulation started and is listening for " +
            "settings from {QueueName}",
            _rabbitMqOptions.SimulationSettingsQueueName);

        try
        {
            await simulation.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // The host requested a graceful shutdown.
        }
        finally
        {
            await settingsChannel.BasicCancelAsync(
                settingsConsumerTag,
                cancellationToken: CancellationToken.None);
        }
    }
}
