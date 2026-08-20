namespace DrillingTelemetry.Processor.Operations;

/// <summary>
/// Represents an operational condition observed while processing telemetry.
/// </summary>
/// <param name="EventId">Unique event identifier.</param>
/// <param name="EventType">Classification of the observed condition.</param>
/// <param name="Severity">Operational severity of the condition.</param>
/// <param name="DeviceId">Device associated with the event, when known.</param>
/// <param name="AcquisitionSessionId">
/// Acquisition session associated with the event, when known.
/// </param>
/// <param name="SequenceNumber">
/// Sequence number that produced the event, when known.
/// </param>
/// <param name="PreviousSequenceNumber">
/// Previous sequence used to classify ordering, when applicable.
/// </param>
/// <param name="GapSize">
/// Number of missing sequence positions, when applicable.
/// </param>
/// <param name="Message">Human-readable operational description.</param>
/// <param name="OccurredAtUtc">UTC time at which the event was observed.</param>
internal sealed record OperationalEvent(
    Guid EventId,
    OperationalEventType EventType,
    OperationalEventSeverity Severity,
    string? DeviceId,
    Guid? AcquisitionSessionId,
    long? SequenceNumber,
    long? PreviousSequenceNumber,
    long? GapSize,
    string Message,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Identifies operational conditions detected by the telemetry processor.
/// </summary>
internal enum OperationalEventType
{
    /// <summary>An identical telemetry reading was received again.</summary>
    DuplicateReading,

    /// <summary>A telemetry identity was reused with different content.</summary>
    ConflictingReading,

    /// <summary>One or more expected sequence numbers were not observed.</summary>
    SequenceGap,

    /// <summary>An older sequence arrived after a newer sequence.</summary>
    OutOfOrderReading,

    /// <summary>An invalid message was rejected by the consumer.</summary>
    InvalidMessage
}

/// <summary>
/// Identifies the operational impact of a telemetry event.
/// </summary>
internal enum OperationalEventSeverity
{
    /// <summary>The event requires awareness but not immediate intervention.</summary>
    Warning,

    /// <summary>The event indicates a data integrity failure.</summary>
    Critical
}
