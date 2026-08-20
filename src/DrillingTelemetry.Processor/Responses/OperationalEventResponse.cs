using DrillingTelemetry.Processor.Operations;

namespace DrillingTelemetry.Processor.Responses;

/// <summary>
/// Represents an operational telemetry event returned to API clients.
/// </summary>
/// <param name="EventId">Unique event identifier.</param>
/// <param name="EventType">Classification of the operational event.</param>
/// <param name="Severity">Operational severity.</param>
/// <param name="DeviceId">Associated device identifier, when known.</param>
/// <param name="AcquisitionSessionId">
/// Associated acquisition session, when known.
/// </param>
/// <param name="SequenceNumber">
/// Sequence that produced the event, when known.
/// </param>
/// <param name="PreviousSequenceNumber">
/// Previous sequence used to classify ordering, when applicable.
/// </param>
/// <param name="GapSize">
/// Number of missing sequence positions, when applicable.
/// </param>
/// <param name="Message">Human-readable operational description.</param>
/// <param name="OccurredAtUtc">UTC time at which the event was observed.</param>
public sealed record OperationalEventResponse(
    Guid EventId,
    string EventType,
    string Severity,
    string? DeviceId,
    Guid? AcquisitionSessionId,
    long? SequenceNumber,
    long? PreviousSequenceNumber,
    long? GapSize,
    string Message,
    DateTimeOffset OccurredAtUtc)
{
    /// <summary>
    /// Maps an internal operational event to its public response contract.
    /// </summary>
    /// <param name="operationalEvent">Event to map.</param>
    /// <returns>The public event response.</returns>
    internal static OperationalEventResponse FromModel(
        OperationalEvent operationalEvent)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);

        return new OperationalEventResponse(
            operationalEvent.EventId,
            operationalEvent.EventType.ToString(),
            operationalEvent.Severity.ToString(),
            operationalEvent.DeviceId,
            operationalEvent.AcquisitionSessionId,
            operationalEvent.SequenceNumber,
            operationalEvent.PreviousSequenceNumber,
            operationalEvent.GapSize,
            operationalEvent.Message,
            operationalEvent.OccurredAtUtc);
    }
}
