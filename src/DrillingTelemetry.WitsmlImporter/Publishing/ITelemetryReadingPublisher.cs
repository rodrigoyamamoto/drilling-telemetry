using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.WitsmlImporter.Publishing;

/// <summary>
/// Publishes telemetry readings to the messaging infrastructure.
/// </summary>
internal interface ITelemetryReadingPublisher
{
    /// <summary>
    /// Publishes a single telemetry reading.
    /// </summary>
    /// <param name="reading">The reading to publish.</param>
    /// <param name="cancellationToken">
    /// Token used to cancel the publish operation.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
