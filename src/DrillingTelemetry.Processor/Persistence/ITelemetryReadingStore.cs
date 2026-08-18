using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.Processor.Persistence;

/// <summary>
/// Persists telemetry readings with durable idempotency.
/// </summary>
internal interface ITelemetryReadingStore
{
    /// <summary>
    /// Stores a reading or classifies an existing reading with the same
    /// device and sequence identity.
    /// </summary>
    /// <param name="reading">Valid telemetry reading to store.</param>
    /// <param name="cancellationToken">
    /// Signals that persistence should be cancelled.
    /// </param>
    /// <returns>The result of the idempotent storage operation.</returns>
    Task<TelemetryReadingStoreResult> StoreAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
