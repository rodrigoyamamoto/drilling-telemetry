using System.Text.Json;
using DrillingTelemetry.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Persists telemetry readings in PostgreSQL using their device and sequence
/// as a durable idempotency key.
/// </summary>
internal sealed class IdempotentTelemetryReadingStore
    : ITelemetryReadingStore
{
    private const string InsertReadingSql =
        """
        INSERT INTO telemetry_readings
        (
            device_id,
            acquisition_session_id,
            sequence_number,
            pressure_psi,
            temperature_celsius,
            timestamp_utc,
            payload
        )
        VALUES ($1, $2, $3, $4, $5, $6, $7)
        ON CONFLICT
        (
            device_id,
            acquisition_session_id,
            sequence_number
        )
        DO NOTHING;
        """;

    private const string CompareExistingPayloadSql =
        """
        SELECT payload = $4
        FROM telemetry_readings
        WHERE device_id = $1
          AND acquisition_session_id = $2
          AND sequence_number = $3;
        """;

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initialises the PostgreSQL telemetry reading store.
    /// </summary>
    /// <param name="dataSource">
    /// Provides pooled PostgreSQL connections.
    /// </param>
    public IdempotentTelemetryReadingStore(
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<TelemetryReadingStoreResult> SaveAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        string payload = JsonSerializer.Serialize(reading);

        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        int insertedRows = await InsertAsync(
            connection,
            reading,
            payload,
            cancellationToken);

        if (insertedRows == 1)
        {
            return TelemetryReadingStoreResult.Stored;
        }

        bool identicalPayload = await HasIdenticalPayloadAsync(
            connection,
            reading,
            payload,
            cancellationToken);

        return identicalPayload
            ? TelemetryReadingStoreResult.Duplicate
            : TelemetryReadingStoreResult.Conflict;
    }

    /// <summary>
    /// Attempts to insert a telemetry reading without replacing an existing
    /// natural key.
    /// </summary>
    /// <param name="connection">Open PostgreSQL connection.</param>
    /// <param name="reading">Telemetry reading to insert.</param>
    /// <param name="payload">Canonical JSON payload used for comparison.</param>
    /// <param name="cancellationToken">
    /// Signals that the insert should be cancelled.
    /// </param>
    /// <returns>The number of inserted rows.</returns>
    private static async Task<int> InsertAsync(
        NpgsqlConnection connection,
        TelemetryReading reading,
        string payload,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            InsertReadingSql,
            connection);

        AddIdentityParameters(command, reading);

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Double,
                Value = reading.PressurePsi
            });

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Double,
                Value = reading.TemperatureCelsius
            });

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.TimestampTz,
                Value = reading.TimestampUtc.UtcDateTime
            });

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Jsonb,
                Value = payload
            });

        return await command.ExecuteNonQueryAsync(
            cancellationToken);
    }

    /// <summary>
    /// Determines whether an existing natural key contains the same payload.
    /// </summary>
    /// <param name="connection">Open PostgreSQL connection.</param>
    /// <param name="reading">Telemetry reading being compared.</param>
    /// <param name="payload">Canonical JSON payload used for comparison.</param>
    /// <param name="cancellationToken">
    /// Signals that the query should be cancelled.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the stored payload is identical;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static async Task<bool> HasIdenticalPayloadAsync(
        NpgsqlConnection connection,
        TelemetryReading reading,
        string payload,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            CompareExistingPayloadSql,
            connection);

        AddIdentityParameters(command, reading);

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Jsonb,
                Value = payload
            });

        object? result = await command.ExecuteScalarAsync(
            cancellationToken);

        return result is true;
    }

    /// <summary>
    /// Adds the natural telemetry identity to a database command.
    /// </summary>
    /// <param name="command">Command receiving the parameters.</param>
    /// <param name="reading">Reading providing the identity.</param>
    private static void AddIdentityParameters(
        NpgsqlCommand command,
        TelemetryReading reading)
    {
        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Text,
                Value = reading.DeviceId
            });

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Uuid,
                Value = reading.AcquisitionSessionId
            });

        command.Parameters.Add(
            new NpgsqlParameter
            {
                NpgsqlDbType = NpgsqlDbType.Bigint,
                Value = reading.SequenceNumber
            });
    }
}
