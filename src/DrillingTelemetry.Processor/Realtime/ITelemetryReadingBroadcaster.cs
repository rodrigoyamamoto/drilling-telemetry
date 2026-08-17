using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts telemetry readings to connected real-time clients.
/// </summary>
internal interface ITelemetryReadingBroadcaster
{
    /// <summary>
    /// Broadcasts a telemetry reading to every connected client.
    /// </summary>
    /// <param name="reading">
    /// Telemetry reading to broadcast.
    /// </param>
    /// <param name="cancellationToken">
    /// Signals that the broadcast should be cancelled.
    /// </param>
    Task BroadcastAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
