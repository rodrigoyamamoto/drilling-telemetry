using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator;

internal interface ITelemetryReadingPublisher
{
    /// <summary>
    /// Publishes a telemetry reading.
    /// </summary>
    /// <param name="reading">Telemetry reading to publish.</param>
    /// <param name="cancellationToken">
    /// Token used to cancel the publishing operation.
    /// </param>
    Task PublishAsync(
        TelemetryReading reading,
        CancellationToken cancellationToken);
}
