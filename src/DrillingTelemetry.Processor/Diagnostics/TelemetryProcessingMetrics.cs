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
}
