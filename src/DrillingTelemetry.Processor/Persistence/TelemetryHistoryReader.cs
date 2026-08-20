using DrillingTelemetry.Contracts;
using Dapper;
using Npgsql;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Reads persisted telemetry history from PostgreSQL.
/// </summary>
internal sealed class TelemetryHistoryReader
    : ITelemetryHistoryReader
{
    private const string SelectDeviceIdsSql =
        """
        SELECT DISTINCT device_id AS "DeviceId"
        FROM telemetry_readings
        ORDER BY device_id;
        """;

    private const string SelectReadingsSql =
        """
        SELECT
            device_id AS "DeviceId",
            acquisition_session_id AS "AcquisitionSessionId",
            sequence_number AS "SequenceNumber",
            well_id AS "WellId",
            wellbore_id AS "WellboreId",
            measured_depth_metres AS "MeasuredDepthMetres",
            drilling_operation AS "DrillingOperation",
            depth_change_rate_metres_per_hour AS "DepthChangeRateMetresPerHour",
            pressure_psi AS "PressurePsi",
            temperature_celsius AS "TemperatureCelsius",
            timestamp_utc AS "TimestampUtc"
        FROM
        (
            SELECT
                device_id,
                acquisition_session_id,
                sequence_number,
                well_id,
                wellbore_id,
                measured_depth_metres,
                drilling_operation,
                depth_change_rate_metres_per_hour,
                pressure_psi,
                temperature_celsius,
                timestamp_utc
            FROM telemetry_readings
            WHERE device_id = @DeviceId
            ORDER BY timestamp_utc DESC, sequence_number DESC
            LIMIT @Limit
        ) AS recent_readings
        ORDER BY timestamp_utc, sequence_number;
        """;

    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Initialises the PostgreSQL telemetry history service.
    /// </summary>
    /// <param name="dataSource">
    /// Provides pooled PostgreSQL connections.
    /// </param>
    public TelemetryHistoryReader(
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetDeviceIdsAsync(
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            SelectDeviceIdsSql,
            cancellationToken: cancellationToken);

        IEnumerable<string> deviceIds =
            await connection.QueryAsync<string>(command);

        return deviceIds.AsList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TelemetryReading>> GetReadingsAsync(
        string deviceId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "The reading limit must be greater than zero.");
        }

        await using NpgsqlConnection connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var command = new CommandDefinition(
            SelectReadingsSql,
            new
            {
                DeviceId = deviceId,
                Limit = limit
            },
            cancellationToken: cancellationToken);

        IEnumerable<TelemetryReading> readings =
            await connection.QueryAsync<TelemetryReading>(
                command);

        return readings.AsList();
    }
}
