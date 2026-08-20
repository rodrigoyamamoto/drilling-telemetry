using DrillingTelemetry.Processor.Diagnostics;

namespace DrillingTelemetry.Processor.Responses;

/// <summary>
/// Represents live telemetry processing metrics delivered to dashboard
/// clients.
/// </summary>
/// <param name="SampledAtUtc">
/// UTC time at which the metrics were calculated.
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
public sealed record TelemetryMetricsResponse(
    DateTimeOffset SampledAtUtc,
    long ReadingsProcessedTotal,
    double ReadingsPerSecond,
    double? LatestEndToEndLatencyMilliseconds)
{
    /// <summary>
    /// Maps an internal metrics snapshot to its real-time response contract.
    /// </summary>
    /// <param name="snapshot">Snapshot to map.</param>
    /// <returns>The dashboard response.</returns>
    internal static TelemetryMetricsResponse FromModel(
        TelemetryMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new TelemetryMetricsResponse(
            snapshot.SampledAtUtc,
            snapshot.ReadingsProcessedTotal,
            snapshot.ReadingsPerSecond,
            snapshot.LatestEndToEndLatencyMilliseconds);
    }
}
