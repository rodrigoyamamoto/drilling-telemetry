using DrillingTelemetry.Contracts;
using DrillingTelemetry.Processor.Diagnostics;
using DrillingTelemetry.Processor.Operations;
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
    private readonly IOperationalEventService _operationalEventService;
    private readonly ITelemetryReadingBroadcaster _telemetryReadingBroadcaster;
    private readonly ConcurrentAcquisitionSessionDetector
        _concurrentSessionDetector;
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
    /// <param name="operationalEventService">
    /// Records anomalies detected while processing telemetry.
    /// </param>
    /// <param name="telemetryReadingBroadcaster">
    /// Broadcasts accepted readings to connected clients.
    /// </param>
    /// <param name="concurrentSessionDetector">
    /// Detects concurrent acquisition sessions for the same device.
    /// </param>
    /// <param name="logger">
    /// Records processing decisions and sequence anomalies.
    /// </param>
    public TelemetryReadingProcessor(
        TimeProvider timeProvider,
        TelemetryProcessingMetrics metrics,
        TelemetrySequenceTracker sequenceTracker,
        ITelemetryReadingStore telemetryReadingStore,
        IOperationalEventService operationalEventService,
        ITelemetryReadingBroadcaster telemetryReadingBroadcaster,
        ConcurrentAcquisitionSessionDetector concurrentSessionDetector,
        ILogger<TelemetryReadingProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(sequenceTracker);
        ArgumentNullException.ThrowIfNull(telemetryReadingStore);
        ArgumentNullException.ThrowIfNull(operationalEventService);
        ArgumentNullException.ThrowIfNull(telemetryReadingBroadcaster);
        ArgumentNullException.ThrowIfNull(concurrentSessionDetector);
        ArgumentNullException.ThrowIfNull(logger);

        _timeProvider = timeProvider;
        _metrics = metrics;
        _sequenceTracker = sequenceTracker;
        _telemetryReadingStore = telemetryReadingStore;
        _operationalEventService = operationalEventService;
        _telemetryReadingBroadcaster = telemetryReadingBroadcaster;
        _concurrentSessionDetector = concurrentSessionDetector;
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
            await RecordDuplicateAsync(
                reading,
                cancellationToken);

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

            await RecordOperationalEventAsync(
                reading,
                OperationalEventType.ConflictingReading,
                OperationalEventSeverity.Critical,
                previousSequenceNumber: null,
                gapSize: null,
                "The sequence identity was reused with " +
                "different telemetry content.",
                cancellationToken);

            return TelemetryProcessingResult.Conflict;
        }

        ConcurrentAcquisitionSessionConflict? sessionConflict =
            _concurrentSessionDetector.Observe(
                reading.DeviceId,
                reading.AcquisitionSessionId);

        if (sessionConflict is not null)
        {
            await RecordConcurrentSessionsAsync(
                reading,
                sessionConflict,
                cancellationToken);
        }

        TelemetrySequenceObservation observation =
            _sequenceTracker.Observe(
                reading.DeviceId,
                reading.AcquisitionSessionId,
                reading.SequenceNumber);

        if (!await ShouldPublishAsync(
                reading,
                observation,
                cancellationToken))
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
    private async Task<bool> ShouldPublishAsync(
        TelemetryReading reading,
        TelemetrySequenceObservation observation,
        CancellationToken cancellationToken)
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

                await RecordOperationalEventAsync(
                    reading,
                    OperationalEventType.SequenceGap,
                    OperationalEventSeverity.Warning,
                    observation.PreviousSequenceNumber,
                    observation.GapSize,
                    $"{observation.GapSize} sequence positions were " +
                    $"skipped after {observation.PreviousSequenceNumber}.",
                    cancellationToken);
                return true;

            case TelemetrySequenceStatus.Duplicate:
                await RecordDuplicateAsync(
                    reading,
                    cancellationToken);
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

                await RecordOperationalEventAsync(
                    reading,
                    OperationalEventType.OutOfOrderReading,
                    OperationalEventSeverity.Warning,
                    observation.PreviousSequenceNumber,
                    gapSize: null,
                    $"Sequence {reading.SequenceNumber} arrived after " +
                    $"sequence {observation.PreviousSequenceNumber} " +
                    "had already been observed.",
                    cancellationToken);
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
    private async Task RecordDuplicateAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        _metrics.RecordDuplicateReading();

        _logger.LogWarning(
            "Duplicate telemetry sequence {SequenceNumber} " +
            "received from {DeviceId}",
            reading.SequenceNumber,
            reading.DeviceId);

        await RecordOperationalEventAsync(
            reading,
            OperationalEventType.DuplicateReading,
            OperationalEventSeverity.Warning,
            previousSequenceNumber: reading.SequenceNumber,
            gapSize: null,
            "An identical reading was ignored.",
            cancellationToken);
    }

    private Task RecordOperationalEventAsync(
        TelemetryReading reading,
        OperationalEventType eventType,
        OperationalEventSeverity severity,
        long? previousSequenceNumber,
        long? gapSize,
        string message,
        CancellationToken cancellationToken)
    {
        var operationalEvent = new OperationalEvent(
            Guid.NewGuid(),
            eventType,
            severity,
            reading.DeviceId,
            reading.AcquisitionSessionId,
            reading.SequenceNumber,
            previousSequenceNumber,
            gapSize,
            message,
            _timeProvider.GetUtcNow());

        return _operationalEventService.RecordAsync(
            operationalEvent,
            cancellationToken);
    }

    /// <summary>
    /// Records that a device is receiving telemetry from two active
    /// acquisition sessions.
    /// </summary>
    /// <param name="reading">
    /// Telemetry reading that triggered the detection.
    /// </param>
    /// <param name="sessionConflict">
    /// Identifies the existing and newly observed acquisition sessions.
    /// </param>
    /// <param name="cancellationToken">
    /// Signals that event recording should be cancelled.
    /// </param>
    private Task RecordConcurrentSessionsAsync(
        TelemetryReading reading,
        ConcurrentAcquisitionSessionConflict sessionConflict,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Concurrent acquisition sessions detected for {DeviceId}: " +
            "existing session {ExistingSessionId}, " +
            "newly observed session {ObservedSessionId}",
            reading.DeviceId,
            sessionConflict.ExistingSessionId,
            sessionConflict.ObservedSessionId);

        string existingRun = FormatRunLabel(
            sessionConflict.ExistingSessionId);
        string observedRun = FormatRunLabel(
            sessionConflict.ObservedSessionId);

        var operationalEvent = new OperationalEvent(
            Guid.NewGuid(),
            OperationalEventType.ConcurrentAcquisitionSessions,
            OperationalEventSeverity.Warning,
            reading.DeviceId,
            reading.AcquisitionSessionId,
            SequenceNumber: null,
            PreviousSequenceNumber: null,
            GapSize: null,
            $"{reading.DeviceId} is receiving telemetry from two " +
            $"active acquisition runs. Existing run {existingRun}, " +
            $"newly observed run {observedRun}.",
            _timeProvider.GetUtcNow());

        return _operationalEventService.RecordAsync(
            operationalEvent,
            cancellationToken);
    }

    private static string FormatRunLabel(Guid sessionId)
    {
        return sessionId.ToString("N").Substring(0, 8)
            .ToUpperInvariant();
    }
}
