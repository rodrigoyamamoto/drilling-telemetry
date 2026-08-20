using Dapper;
using DrillingTelemetry.Processor.Operations;
using Npgsql;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Persists operational telemetry events in PostgreSQL.
/// </summary>
internal sealed class OperationalEventStore
    : IOperationalEventStore
{
    private const string InsertEventSql =
        """
        INSERT INTO operational_events
        (
            event_id,
            event_type,
            severity,
            device_id,
            acquisition_session_id,
            sequence_number,
            previous_sequence_number,
            gap_size,
            message,
            occurred_at_utc
        )
        VALUES
        (
            @EventId,
            @EventType,
            @Severity,
            @DeviceId,
            @AcquisitionSessionId,
            @SequenceNumber,
            @PreviousSequenceNumber,
            @GapSize,
            @Message,
            @OccurredAtUtc
        );
        """;

    private const string SelectRecentEventsSql =
        """
        SELECT
            event_id AS "EventId",
            event_type AS "EventType",
            severity AS "Severity",
            device_id AS "DeviceId",
            acquisition_session_id AS "AcquisitionSessionId",
            sequence_number AS "SequenceNumber",
            previous_sequence_number AS "PreviousSequenceNumber",
            gap_size AS "GapSize",
            message AS "Message",
            occurred_at_utc AS "OccurredAtUtc"
        FROM operational_events
        ORDER BY occurred_at_utc DESC, event_id DESC
        LIMIT @Limit;
        """;

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initialises the operational event store.
    /// </summary>
    /// <param name="dataSource">
    /// Provides pooled PostgreSQL connections.
    /// </param>
    public OperationalEventStore(
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        OperationalEvent operationalEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);

        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            InsertEventSql,
            new
            {
                operationalEvent.EventId,
                EventType = operationalEvent.EventType.ToString(),
                Severity = operationalEvent.Severity.ToString(),
                operationalEvent.DeviceId,
                operationalEvent.AcquisitionSessionId,
                operationalEvent.SequenceNumber,
                operationalEvent.PreviousSequenceNumber,
                operationalEvent.GapSize,
                operationalEvent.Message,
                OccurredAtUtc = operationalEvent.OccurredAtUtc.UtcDateTime
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OperationalEvent>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "The event limit must be greater than zero.");
        }

        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            SelectRecentEventsSql,
            new { Limit = limit },
            cancellationToken: cancellationToken);

        IEnumerable<OperationalEventRow> rows =
            await connection.QueryAsync<OperationalEventRow>(
                command);

        return rows
            .Select(MapEvent)
            .ToList();
    }

    private static OperationalEvent MapEvent(
        OperationalEventRow row)
    {
        return new OperationalEvent(
            row.EventId,
            Enum.Parse<OperationalEventType>(row.EventType),
            Enum.Parse<OperationalEventSeverity>(row.Severity),
            row.DeviceId,
            row.AcquisitionSessionId,
            row.SequenceNumber,
            row.PreviousSequenceNumber,
            row.GapSize,
            row.Message,
            row.OccurredAtUtc);
    }

    private sealed class OperationalEventRow
    {
        /// <summary>Gets the unique event identifier.</summary>
        public Guid EventId { get; init; }

        /// <summary>Gets the persisted event type.</summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>Gets the persisted severity.</summary>
        public string Severity { get; init; } = string.Empty;

        /// <summary>Gets the associated device identifier.</summary>
        public string? DeviceId { get; init; }

        /// <summary>Gets the associated acquisition session.</summary>
        public Guid? AcquisitionSessionId { get; init; }

        /// <summary>Gets the associated sequence number.</summary>
        public long? SequenceNumber { get; init; }

        /// <summary>Gets the preceding sequence number.</summary>
        public long? PreviousSequenceNumber { get; init; }

        /// <summary>Gets the number of skipped sequence positions.</summary>
        public long? GapSize { get; init; }

        /// <summary>Gets the operational description.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the event observation time.</summary>
        public DateTimeOffset OccurredAtUtc { get; init; }
    }
}
