using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Publishing;

namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Coordinates the generation and publishing of telemetry readings.
/// </summary>
internal sealed class TelemetrySimulation
{
    private readonly ITelemetryReadingGenerator _readingGenerator;
    private readonly ITelemetryReadingPublisher _readingPublisher;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a telemetry simulation.
    /// </summary>
    /// <param name="readingGenerator">
    /// Generator used to create telemetry readings.
    /// </param>
    /// <param name="readingPublisher">
    /// Publisher used to send telemetry readings.
    /// </param>
    /// // <param name="timeProvider">
    /// Provides the timer used between publishing cycles.
    /// </param>
    public TelemetrySimulation(
        ITelemetryReadingGenerator readingGenerator,
        ITelemetryReadingPublisher readingPublisher,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(readingGenerator);
        ArgumentNullException.ThrowIfNull(readingPublisher);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _readingGenerator = readingGenerator;
        _readingPublisher = readingPublisher;
        _timeProvider = timeProvider;
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
        IReadOnlyList<string> deviceIds,
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

    /// <summary>
    /// Continuously publishes telemetry cycles using the specified interval.
    /// </summary>
    /// <param name="deviceIds">
    /// Identifiers of the devices included in each cycle.
    /// </param>
    /// <param name="publishingInterval">
    /// Time waited between publishing cycles.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to stop the simulation.
    /// </param>
    public async Task RunAsync(
        IReadOnlyList<string> deviceIds,
        TimeSpan publishingInterval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deviceIds);

        while (!cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await PublishCycleAsync(deviceIds, cancellationToken);
            await Task.Delay(publishingInterval, _timeProvider, cancellationToken);
        }
    }
}
