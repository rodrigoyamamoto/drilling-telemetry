namespace DrillingTelemetry.Processor.Diagnostics;

/// <summary>
/// Represents a point-in-time view of live telemetry processing performance.
/// </summary>
/// <param name="SampledAtUtc">
/// UTC time at which the snapshot was calculated.
/// </param>
/// <param name="ReadingsProcessedTotal">
/// Number of readings accepted for the live stream since processor startup.
/// </param>
/// <param name="ReadingsPerSecond">
/// Accepted readings processed per second during the latest interval.
/// </param>
/// <param name="LatestEndToEndLatencyMilliseconds">
/// Latest measured duration from acquisition to processing completion.
/// </param>
internal sealed record TelemetryMetricsSnapshot(
    DateTimeOffset SampledAtUtc,
    long ReadingsProcessedTotal,
    double ReadingsPerSecond,
    double? LatestEndToEndLatencyMilliseconds);
