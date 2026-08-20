namespace DrillingTelemetry.Processor.Operations;

/// <summary>
/// Records and provides operational telemetry events.
/// </summary>
internal interface IOperationalEventService
{
    /// <summary>Persists and publishes an operational event.</summary>
    /// <param name="operationalEvent">Event to record.</param>
    /// <param name="cancellationToken">
    /// Signals that event recording should be cancelled.
    /// </param>
    Task RecordAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken);

    /// <summary>Gets the most recent operational events.</summary>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">
    /// Signals that the query should be cancelled.
    /// </param>
    /// <returns>The events in reverse chronological order.</returns>
    Task<IReadOnlyList<OperationalEvent>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken);
}
