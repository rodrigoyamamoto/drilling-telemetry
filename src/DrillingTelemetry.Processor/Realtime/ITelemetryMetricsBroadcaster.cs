using DrillingTelemetry.Processor.Diagnostics;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts telemetry processing metrics to connected clients.
/// </summary>
internal interface ITelemetryMetricsBroadcaster
{
    /// <summary>Broadcasts a metrics snapshot.</summary>
    /// <param name="snapshot">Metrics snapshot to broadcast.</param>
    /// <param name="cancellationToken">
    /// Signals that the broadcast should be cancelled.
    /// </param>
    Task BroadcastAsync(
        TelemetryMetricsSnapshot snapshot,
        CancellationToken cancellationToken);
}
