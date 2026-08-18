using System.Diagnostics.Metrics;

namespace DrillingTelemetry.Processor.Diagnostics;

/// <summary>
/// Records metrics produced while telemetry readings are processed.
/// </summary>
internal sealed class TelemetryProcessingMetrics
{
    /// <summary>
    /// Identifies the telemetry processor meter.
    /// </summary>
    public const string MeterName =
        "DrillingTelemetry.Processor";

    private readonly Counter<long> _readingsProcessed;
    private readonly Counter<long> _invalidMessages;
    private readonly Counter<long> _missingReadings;
    private readonly Counter<long> _duplicateReadings;
    private readonly Counter<long> _outOfOrderReadings;
    private readonly Histogram<double> _endToEndDuration;

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

        _missingReadings = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.missing",
            unit: "{reading}",
            description:
                "Number of telemetry readings missing from sequences.");

        _duplicateReadings = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.duplicate",
            unit: "{reading}",
            description:
                "Number of duplicate telemetry readings received.");

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

        if (endToEndDuration >= TimeSpan.Zero)
        {
            _endToEndDuration.Record(
                endToEndDuration.TotalSeconds);
        }
    }

    /// <summary>
    /// Records an invalid telemetry message.
    /// </summary>
    public void RecordInvalidMessage()
    {
        _invalidMessages.Add(1);
    }

    /// <summary>
    /// Records telemetry readings missing from a device sequence.
    /// </summary>
    /// <param name="missingReadingCount">
    /// Number of readings missing from the observed sequence.
    /// </param>
    public void RecordMissingReadings(long missingReadingCount)
    {
        _missingReadings.Add(missingReadingCount);
    }

    /// <summary>
    /// Records a duplicate telemetry reading.
    /// </summary>
    public void RecordDuplicateReading()
    {
        _duplicateReadings.Add(1);
    }

    /// <summary>
    /// Records an out-of-order telemetry reading.
    /// </summary>
    public void RecordOutOfOrderReading()
    {
        _outOfOrderReadings.Add(1);
    }
}
