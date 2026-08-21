using System.Collections.Concurrent;
using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator.Generation;
using DrillingTelemetry.DeviceSimulator.Publishing;

namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Coordinates the generation and publishing of telemetry readings.
/// </summary>
internal sealed class TelemetrySimulation
{
    private readonly ConcurrentDictionary<string, long>
        _sequenceNumbers = new(StringComparer.Ordinal);

    private readonly Guid _acquisitionSessionId = Guid.NewGuid();

    private readonly ITelemetryReadingGenerator _readingGenerator;
    private readonly ITelemetryReadingPublisher _readingPublisher;

    private readonly TimeProvider _timeProvider;
    private readonly SimulationSettingsState _settingsState;
    private readonly SimulationDrillingContext _drillingContext;

    /// <summary>
    /// Initialises a telemetry simulation.
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
    /// <param name="settingsState">
    /// Provides the current runtime simulation settings.
    /// </param>
    /// <param name="drillingContext">
    /// Identifies the well, wellbore and measured depth being simulated.
    /// </param>
    public TelemetrySimulation(
        ITelemetryReadingGenerator readingGenerator,
        ITelemetryReadingPublisher readingPublisher,
        TimeProvider timeProvider,
        SimulationSettingsState settingsState,
        SimulationDrillingContext drillingContext)
    {
        ArgumentNullException.ThrowIfNull(readingGenerator);
        ArgumentNullException.ThrowIfNull(readingPublisher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settingsState);
        ArgumentNullException.ThrowIfNull(drillingContext);

        _readingGenerator = readingGenerator;
        _readingPublisher = readingPublisher;
        _timeProvider = timeProvider;
        _settingsState = settingsState;
        _drillingContext = drillingContext;
    }

    /// <summary>
    /// Generates and publishes one telemetry reading for each device.
    /// </summary>
    /// <param name="settings">
    /// Immutable settings snapshot applied to the complete cycle.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the publishing operation.
    /// </param>
    public async Task PublishCycleAsync(
        SimulationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        double measuredDepthMetres =
            _drillingContext.MeasuredDepthMetres;

        foreach (string deviceId in settings.DeviceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TelemetryReading reading = _readingGenerator.Generate(
                deviceId,
                measuredDepthMetres);
            reading.AcquisitionSessionId = _acquisitionSessionId;
            reading.SequenceNumber = GetNextSequenceNumber(deviceId);
            reading.WellId = _drillingContext.WellId;
            reading.WellName = _drillingContext.WellName;
            reading.WellboreId = _drillingContext.WellboreId;
            reading.WellboreName = _drillingContext.WellboreName;
            reading.DrillingOperation = settings.DrillingOperation;
            reading.DepthChangeRateMetresPerHour =
                settings.DepthChangeRateMetresPerHour;

            await _readingPublisher.PublishAsync(reading, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the next sequence number for the specified device.
    /// </summary>
    /// <param name="deviceId">
    /// Identifier of the device whose sequence is advanced.
    /// </param>
    /// <returns>The next sequence number for the device.</returns>
    private long GetNextSequenceNumber(string deviceId)
    {
        return _sequenceNumbers.AddOrUpdate(
            deviceId,
            addValue: 1,
            updateValueFactory: static (_, currentSequenceNumber) =>
                checked(currentSequenceNumber + 1));
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
            SimulationSettings settings = _settingsState.Current;

            await PublishCycleAsync(settings, cancellationToken);

            TimeSpan elapsed = await WaitForNextCycleAsync(
                settings,
                cancellationToken);

            _drillingContext.Advance(
                settings.DrillingOperation,
                settings.DepthChangeRateMetresPerHour,
                elapsed);
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
    /// <returns>
    /// Actual time elapsed before the interval completed or settings changed.
    /// </returns>
    private async Task<TimeSpan> WaitForNextCycleAsync(
        SimulationSettings settings,
        CancellationToken cancellationToken)
    {
        long startedAtTimestamp = _timeProvider.GetTimestamp();

        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var delayTask = Task.Delay(settings.PublishingInterval, _timeProvider, waitCancellation.Token);
        Task settingsChangedTask = _settingsState.WaitForChangeAsync(settings.Revision, waitCancellation.Token);

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

        return _timeProvider.GetElapsedTime(startedAtTimestamp);
    }
}
