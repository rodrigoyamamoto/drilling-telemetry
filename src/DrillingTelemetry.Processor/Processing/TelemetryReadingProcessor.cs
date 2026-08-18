using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Persistence;
using DrillingTelemetry.Processor.Realtime;
using DrillingTelemetry.Processor.Sequencing;

namespace DrillingTelemetry.Processor.Processing;

/// <summary>
/// Applies sequence, observability and real-time publication rules to valid
/// telemetry readings.
/// </summary>
internal sealed class TelemetryReadingProcessor
    : ITelemetryReadingProcessor
{
    private readonly TimeProvider _timeProvider;
    private readonly TelemetryProcessingMetrics _metrics;
    private readonly TelemetrySequenceTracker _sequenceTracker;
    private readonly ITelemetryReadingStore _telemetryReadingStore;
    private readonly ITelemetryReadingBroadcaster _telemetryReadingBroadcaster;
    private readonly ILogger<TelemetryReadingProcessor> _logger;

    /// <summary>
    /// Initialises the telemetry reading processor.
    /// </summary>
    /// <param name="timeProvider">
    /// Provides the current UTC time used to calculate latency.
    /// </param>
    /// <param name="metrics">Records telemetry processing measurements.</param>
    /// <param name="sequenceTracker">
    /// Tracks the sequence observed for each telemetry device.
    /// </param>
    /// <param name="telemetryReadingStore">
    /// Persists readings using their durable idempotency key.
    /// </param>
    /// <param name="telemetryReadingBroadcaster">
    /// Broadcasts accepted readings to connected clients.
    /// </param>
    /// <param name="logger">
    /// Records processing decisions and sequence anomalies.
    /// </param>
    public TelemetryReadingProcessor(
        TimeProvider timeProvider,
        TelemetryProcessingMetrics metrics,
        TelemetrySequenceTracker sequenceTracker,
        ITelemetryReadingStore telemetryReadingStore,
        ITelemetryReadingBroadcaster telemetryReadingBroadcaster,
        ILogger<TelemetryReadingProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(sequenceTracker);
        ArgumentNullException.ThrowIfNull(telemetryReadingStore);
        ArgumentNullException.ThrowIfNull(telemetryReadingBroadcaster);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _metrics = metrics;
        _sequenceTracker = sequenceTracker;
        _telemetryReadingStore = telemetryReadingStore;
        _telemetryReadingBroadcaster = telemetryReadingBroadcaster;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TelemetryProcessingResult> ProcessAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        TelemetryReadingStoreResult storeResult =
            await _telemetryReadingStore.SaveAsync(
                reading,
                cancellationToken);

        if (storeResult == TelemetryReadingStoreResult.Duplicate)
        {
            RecordDuplicate(reading);

            return TelemetryProcessingResult.Duplicate;
        }

        if (storeResult == TelemetryReadingStoreResult.Conflict)
        {
            _metrics.RecordConflictingReading();

            _logger.LogError(
                "Conflicting telemetry content received for " +
                "{DeviceId} sequence {SequenceNumber}",
                reading.DeviceId,
                reading.SequenceNumber);

            return TelemetryProcessingResult.Conflict;
        }

        TelemetrySequenceObservation observation =
            _sequenceTracker.Observe(
                reading.DeviceId,
                reading.AcquisitionSessionId,
                reading.SequenceNumber);

        if (!ShouldPublish(reading, observation))
        {
            return observation.Status ==
                TelemetrySequenceStatus.OutOfOrder
                    ? TelemetryProcessingResult.LateArrival
                    : TelemetryProcessingResult.Duplicate;
        }

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
            cancellationToken);

        _metrics.RecordReadingProcessed(
            _timeProvider.GetUtcNow() - reading.TimestampUtc);

        return TelemetryProcessingResult.Published;
    }

    /// <summary>
    /// Records the sequence observation and decides whether the reading may
    /// advance the real-time stream.
    /// </summary>
    /// <param name="reading">Telemetry reading being processed.</param>
    /// <param name="observation">
    /// Result of comparing the reading with the last observed sequence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the reading advances the device sequence;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool ShouldPublish(
        TelemetryReading reading,
        TelemetrySequenceObservation observation)
    {
        switch (observation.Status)
        {
            case TelemetrySequenceStatus.Gap:
                _metrics.RecordSequenceGap(observation.GapSize);

                _logger.LogWarning(
                    "Telemetry sequence gap detected for {DeviceId}: " +
                    "received {SequenceNumber} after " +
                    "{PreviousSequenceNumber}; " +
                    "{GapSize} sequence numbers were skipped",
                    reading.DeviceId,
                    reading.SequenceNumber,
                    observation.PreviousSequenceNumber,
                    observation.GapSize);
                return true;

            case TelemetrySequenceStatus.Duplicate:
                RecordDuplicate(reading);
                return false;

            case TelemetrySequenceStatus.OutOfOrder:
                _metrics.RecordOutOfOrderReading();

                _logger.LogWarning(
                    "Out-of-order telemetry sequence {SequenceNumber} " +
                    "received from {DeviceId} after " +
                    "{PreviousSequenceNumber}",
                    reading.SequenceNumber,
                    reading.DeviceId,
                    observation.PreviousSequenceNumber);
                return false;

            case TelemetrySequenceStatus.Baseline:
            case TelemetrySequenceStatus.InOrder:
                return true;

            default:
                throw new InvalidOperationException(
                    $"Unsupported telemetry sequence status " +
                    $"'{observation.Status}'.");
        }
    }

    /// <summary>
    /// Records an identical telemetry reading that requires no further work.
    /// </summary>
    /// <param name="reading">Duplicate telemetry reading.</param>
    private void RecordDuplicate(TelemetryReading reading)
    {
        _metrics.RecordDuplicateReading();

        _logger.LogWarning(
            "Duplicate telemetry sequence {SequenceNumber} " +
            "received from {DeviceId}",
            reading.SequenceNumber,
            reading.DeviceId);
    }
}
