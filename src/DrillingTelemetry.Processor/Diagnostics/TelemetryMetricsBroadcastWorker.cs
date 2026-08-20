using DrillingTelemetry.Processor.Realtime;

namespace DrillingTelemetry.Processor.Diagnostics;

/// <summary>
/// Calculates and broadcasts live telemetry processing metrics at a stable
/// interval.
/// </summary>
internal sealed class TelemetryMetricsBroadcastWorker
    : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval =
        TimeSpan.FromSeconds(1);

    private readonly TimeProvider _timeProvider;
    private readonly TelemetryProcessingMetrics _metrics;
    private readonly ITelemetryMetricsBroadcaster _broadcaster;
    private readonly ILogger<TelemetryMetricsBroadcastWorker> _logger;

    /// <summary>
    /// Initialises the telemetry metrics broadcast worker.
    /// </summary>
    /// <param name="timeProvider">
    /// Provides timestamps and timer scheduling.
    /// </param>
    /// <param name="metrics">
    /// Provides current telemetry processing totals.
    /// </param>
    /// <param name="broadcaster">
    /// Publishes snapshots to connected dashboard clients.
    /// </param>
    /// <param name="logger">
    /// Records failures while broadcasting metrics.
    /// </param>
    public TelemetryMetricsBroadcastWorker(
        TimeProvider timeProvider,
        TelemetryProcessingMetrics metrics,
        ITelemetryMetricsBroadcaster broadcaster,
        ILogger<TelemetryMetricsBroadcastWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _metrics = metrics;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        TelemetryMetricsTotals previousTotals =
            _metrics.GetTotals();

        DateTimeOffset previousSampledAtUtc =
            _timeProvider.GetUtcNow();

        using var timer = new PeriodicTimer(
            BroadcastInterval,
            _timeProvider);

        while (await timer.WaitForNextTickAsync(
                   stoppingToken))
        {
            DateTimeOffset sampledAtUtc =
                _timeProvider.GetUtcNow();

            TelemetryMetricsTotals currentTotals =
                _metrics.GetTotals();

            double elapsedSeconds =
                (sampledAtUtc - previousSampledAtUtc)
                .TotalSeconds;

            double readingsPerSecond = elapsedSeconds > 0
                ? (currentTotals.ReadingsProcessedTotal -
                    previousTotals.ReadingsProcessedTotal) /
                  elapsedSeconds
                : 0;

            var snapshot = new TelemetryMetricsSnapshot(
                sampledAtUtc,
                currentTotals.ReadingsProcessedTotal,
                readingsPerSecond,
                currentTotals.LatestEndToEndLatencyMilliseconds);

            await BroadcastAsync(
                snapshot,
                stoppingToken);

            previousTotals = currentTotals;
            previousSampledAtUtc = sampledAtUtc;
        }
    }

    private async Task BroadcastAsync(
        TelemetryMetricsSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
        {
            await _broadcaster.BroadcastAsync(
                snapshot,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The host requested a graceful shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to broadcast telemetry processing metrics");
        }
    }
}
