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
    private readonly SimulationSettingsStore _settingsStore;

    /// <summary>
    /// Initializes a telemetry simulation.
    /// </summary>
    /// <param name="readingGenerator">
    /// Generator used to create telemetry readings.
    /// </param>
    /// <param name="readingPublisher">
    /// Publisher used to send telemetry readings.
    /// </param>
    /// <param name="timeProvider">
    /// Provides the timer used between publishing cycles.
    /// </param>
    /// <param name="settingsStore">
    /// Provides the current runtime simulation settings.
    /// </param>
    public TelemetrySimulation(
        ITelemetryReadingGenerator readingGenerator,
        ITelemetryReadingPublisher readingPublisher,
        TimeProvider timeProvider,
        SimulationSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(readingGenerator);
        ArgumentNullException.ThrowIfNull(readingPublisher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settingsStore);

        _readingGenerator = readingGenerator;
        _readingPublisher = readingPublisher;
        _timeProvider = timeProvider;
        _settingsStore = settingsStore;
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
    /// Continuously publishes telemetry cycles using the current runtime settings.
    /// </summary>
    /// <param name="cancellationToken">
    /// Token used to stop the simulation.
    /// </param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SimulationSettings settings = _settingsStore.Current;

            await PublishCycleAsync(settings.DeviceIds, cancellationToken);
            await WaitForNextCycleAsync(settings, cancellationToken);
        }
    }

    /// <summary>
    /// Waits until the current interval elapses or settings change.
    /// </summary>
    /// <param name="settings">
    /// Settings used by the cycle that has just completed.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to stop the simulation.
    /// </param>
    private async Task WaitForNextCycleAsync(
        SimulationSettings settings,
        CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var delayTask = Task.Delay(settings.PublishingInterval, _timeProvider, waitCancellation.Token);
        Task settingsChangedTask = _settingsStore.WaitForChangeAsync(settings.Version, waitCancellation.Token);

        await Task.WhenAny(
            delayTask,
            settingsChangedTask);

        await waitCancellation.CancelAsync();

        try
        {
            await Task.WhenAll(delayTask, settingsChangedTask);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // The unfinished internal wait was cancelled intentionally.
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
