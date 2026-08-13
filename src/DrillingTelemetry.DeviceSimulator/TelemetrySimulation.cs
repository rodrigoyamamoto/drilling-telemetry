using DrillingTelemetry.Contracts;

namespace DrillingTelemetry.DeviceSimulator;

/// <summary>
/// Coordinates the generation and publishing of telemetry readings.
/// </summary>
internal sealed class TelemetrySimulation
{
    private readonly ITelemetryReadingGenerator _readingGenerator;
    private readonly ITelemetryReadingPublisher _readingPublisher;

    /// <summary>
    /// Initializes a telemetry simulation.
    /// </summary>
    /// <param name="readingGenerator">
    /// Generator used to create telemetry readings.
    /// </param>
    /// <param name="readingPublisher">
    /// Publisher used to send telemetry readings.
    /// </param>
    public TelemetrySimulation(
        ITelemetryReadingGenerator readingGenerator,
        ITelemetryReadingPublisher readingPublisher)
    {
        ArgumentNullException.ThrowIfNull(readingGenerator);
        ArgumentNullException.ThrowIfNull(readingPublisher);

        _readingGenerator = readingGenerator;
        _readingPublisher = readingPublisher;
    }

    /// <summary>
    /// Generates and publishes one telemetry reading for each device.
    /// </summary>
    /// <param name="deviceIds">
    /// Identifiers of the devices included in the cycle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the publishing operation.
    /// </param>
    public async Task PublishCycleAsync(
        IEnumerable<string> deviceIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        foreach (string deviceId in deviceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TelemetryReading reading = _readingGenerator.Generate(deviceId);
            await _readingPublisher.PublishAsync(reading, cancellationToken);
        }
    }
}