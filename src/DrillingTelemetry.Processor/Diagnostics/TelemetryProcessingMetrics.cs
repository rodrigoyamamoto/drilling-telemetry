using System.Diagnostics.Metrics;

namespace DrillingTelemetry.Processor.Diagnostics;

/// <summary>
/// Records metrics produced while telemetry readings are processed.
/// </summary>
internal sealed class TelemetryProcessingMetrics
{
    private const long NoLatencyRecorded = -1;

    /// <summary>
    /// Identifies the telemetry processor meter.
    /// </summary>
    public const string MeterName =
        "DrillingTelemetry.Processor";

    private readonly Counter<long> _readingsProcessed;
    private readonly Counter<long> _invalidMessages;
    private readonly Counter<long> _sequenceGaps;
    private readonly Counter<long> _duplicateReadings;
    private readonly Counter<long> _conflictingReadings;
    private readonly Counter<long> _outOfOrderReadings;
    private readonly Histogram<double> _endToEndDuration;
    private readonly Histogram<long> _sequenceGapSize;

    private long _readingsProcessedTotal;
    private long _latestEndToEndDurationTicks =
        NoLatencyRecorded;

    /// <summary>
    /// Initialises the telemetry processing metrics.
    /// </summary>
    /// <param name="meterFactory">
    /// Creates the meter and manages its lifetime.
    /// </param>
    public TelemetryProcessingMetrics(
        IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _readingsProcessed = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.processed",
            unit: "{reading}",
            description:
                "Number of telemetry readings processed.");

        _invalidMessages = meter.CreateCounter<long>(
            name: "drilling.telemetry.messages.invalid",
            unit: "{message}",
            description:
                "Number of invalid telemetry messages received.");

        _sequenceGaps = meter.CreateCounter<long>(
            name: "drilling.telemetry.sequence.gaps",
            unit: "{gap}",
            description:
                "Number of telemetry sequence gaps observed.");

        _sequenceGapSize = meter.CreateHistogram<long>(
            name: "drilling.telemetry.sequence.gap.size",
            unit: "{sequence_number}",
            description:
                "Number of sequence positions skipped by an observed gap.");

        _duplicateReadings = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.duplicate",
            unit: "{reading}",
            description:
                "Number of duplicate telemetry readings received.");

        _conflictingReadings = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.conflicting",
            unit: "{reading}",
            description:
                "Number of readings that reused an identity with " +
                "different content.");

        _outOfOrderReadings = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.out_of_order",
            unit: "{reading}",
            description:
                "Number of out-of-order telemetry readings received.");

        _endToEndDuration = meter.CreateHistogram<double>(
            name: "drilling.telemetry.end_to_end.duration",
            unit: "s",
            description:
                "Time from telemetry acquisition to processing completion.");
    }

    /// <summary>
    /// Records a successfully processed telemetry reading.
    /// </summary>
    /// <param name="endToEndDuration">
    /// Time from telemetry acquisition to processing completion.
    /// </param>
    public void RecordReadingProcessed(
        TimeSpan endToEndDuration)
    {
        _readingsProcessed.Add(1);
        Interlocked.Increment(
            ref _readingsProcessedTotal);

        if (endToEndDuration >= TimeSpan.Zero)
        {
            _endToEndDuration.Record(
                endToEndDuration.TotalSeconds);

            Interlocked.Exchange(
                ref _latestEndToEndDurationTicks,
                endToEndDuration.Ticks);
        }
    }

    /// <summary>
    /// Captures the cumulative accepted-reading count and latest measured
    /// end-to-end latency.
    /// </summary>
    /// <returns>
    /// Current values used to build a live operational snapshot.
    /// </returns>
    public TelemetryMetricsTotals GetTotals()
    {
        long latencyTicks = Interlocked.Read(
            ref _latestEndToEndDurationTicks);

        double? latestLatencyMilliseconds =
            latencyTicks == NoLatencyRecorded
                ? null
                : TimeSpan
                    .FromTicks(latencyTicks)
                    .TotalMilliseconds;

        return new TelemetryMetricsTotals(
            Interlocked.Read(
                ref _readingsProcessedTotal),
            latestLatencyMilliseconds);
    }

    /// <summary>
    /// Records an invalid telemetry message.
    /// </summary>
    public void RecordInvalidMessage()
    {
        _invalidMessages.Add(1);
    }

    /// <summary>
    /// Records an observed gap in a device sequence.
    /// </summary>
    /// <param name="gapSize">
    /// Number of sequence positions skipped by the observed gap.
    /// </param>
    public void RecordSequenceGap(long gapSize)
    {
        _sequenceGaps.Add(1);
        _sequenceGapSize.Record(gapSize);
    }

    /// <summary>
    /// Records a duplicate telemetry reading.
    /// </summary>
    public void RecordDuplicateReading()
    {
        _duplicateReadings.Add(1);
    }

    /// <summary>
    /// Records a reading that conflicts with a stored telemetry identity.
    /// </summary>
    public void RecordConflictingReading()
    {
        _conflictingReadings.Add(1);
    }

    /// <summary>
    /// Records an out-of-order telemetry reading.
    /// </summary>
    public void RecordOutOfOrderReading()
    {
        _outOfOrderReadings.Add(1);
    }
}

/// <summary>
/// Contains cumulative values captured from telemetry processing.
/// </summary>
/// <param name="ReadingsProcessedTotal">
/// Number of readings accepted for the live stream since processor startup.
/// </param>
/// <param name="LatestEndToEndLatencyMilliseconds">
/// Latest measured duration from acquisition to processing completion.
/// </param>
internal readonly record struct TelemetryMetricsTotals(
    long ReadingsProcessedTotal,
    double? LatestEndToEndLatencyMilliseconds);
