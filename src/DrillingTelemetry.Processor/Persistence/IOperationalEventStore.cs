using DrillingTelemetry.Processor.Operations;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Persists and reads operational telemetry events.
/// </summary>
internal interface IOperationalEventStore
{
    /// <summary>Persists an operational event.</summary>
    /// <param name="operationalEvent">Event to persist.</param>
    /// <param name="cancellationToken">
    /// Signals that persistence should be cancelled.
    /// </param>
    Task SaveAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken);

    /// <summary>Gets the most recent operational events.</summary>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">
    /// Signals that the database query should be cancelled.
    /// </param>
    /// <returns>The events in reverse chronological order.</returns>
    Task<IReadOnlyList<OperationalEvent>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
