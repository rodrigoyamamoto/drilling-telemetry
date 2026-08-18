using System.Diagnostics.Metrics;

namespace DrillingTelemetry.DeviceSimulator.Diagnostics;

/// <summary>
/// Records metrics produced while telemetry readings are published.
/// </summary>
internal sealed class TelemetryPublishingMetrics
{
    /// <summary>
    /// Identifies the device simulator meter.
    /// </summary>
    public const string MeterName =
        "DrillingTelemetry.DeviceSimulator";

    private readonly Counter<long> _readingsPublished;
    private readonly Histogram<double> _publishDuration;

    /// <summary>
    /// Initialises the telemetry publishing metrics.
    /// </summary>
    /// <param name="meterFactory">
    /// Creates the meter and manages its lifetime.
    /// </param>
    public TelemetryPublishingMetrics(
        IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _readingsPublished = meter.CreateCounter<long>(
            name: "drilling.telemetry.readings.published",
            unit: "{reading}",
            description:
                "Number of telemetry readings published.");

        _publishDuration = meter.CreateHistogram<double>(
            name: "drilling.telemetry.publish.duration",
            unit: "s",
            description:
                "Time spent publishing a telemetry reading.");
    }

    /// <summary>
    /// Records a successfully published telemetry reading.
    /// </summary>
    /// <param name="duration">
    /// Time spent publishing the reading.
    /// </param>
    public void RecordReadingPublished(TimeSpan duration)
    {
        _readingsPublished.Add(1);
        _publishDuration.Record(duration.TotalSeconds);
    }
}
