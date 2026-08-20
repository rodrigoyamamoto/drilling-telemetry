using DrillingTelemetry.Processor.Operations;

namespace DrillingTelemetry.Processor.Realtime;

/// <summary>
/// Broadcasts operational telemetry events to connected clients.
/// </summary>
internal interface IOperationalEventBroadcaster
{
    /// <summary>Broadcasts an operational event.</summary>
    /// <param name="operationalEvent">Event to broadcast.</param>
    /// <param name="cancellationToken">
    /// Signals that the broadcast should be cancelled.
    /// </param>
    Task BroadcastAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken);
}
