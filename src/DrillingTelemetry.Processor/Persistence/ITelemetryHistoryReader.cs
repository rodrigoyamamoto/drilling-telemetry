using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Provides persisted telemetry history for API consumers.
/// </summary>
internal interface ITelemetryHistoryReader
{
    /// <summary>
    /// Gets the identifiers of devices that have persisted readings.
    /// </summary>
    /// <param name="cancellationToken">
    /// Signals that the database query should be cancelled.
    /// </param>
    /// <returns>The device identifiers in ascending order.</returns>
    Task<IReadOnlyList<string>> GetDeviceIdsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the most recent persisted readings for a device, scoped to the
    /// latest acquisition session identified by the newest acquisition
    /// timestamp.
    /// </summary>
    /// <param name="deviceId">Identifier of the telemetry device.</param>
    /// <param name="limit">Maximum number of readings to return.</param>
    /// <param name="cancellationToken">
    /// Signals that the database query should be cancelled.
    /// </param>
    /// <returns>
    /// The readings of the latest acquisition session in chronological order.
    /// </returns>
    Task<IReadOnlyList<TelemetryReading>> GetReadingsAsync(
        string deviceId,
        int limit,
        CancellationToken cancellationToken);
}
