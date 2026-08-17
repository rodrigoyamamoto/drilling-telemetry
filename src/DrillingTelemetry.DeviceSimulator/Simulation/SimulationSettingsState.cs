namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Maintains and signals changes to the current simulation settings.
/// </summary>
internal sealed class SimulationSettingsState
{
    private readonly object _syncRoot = new();

    private SimulationSettings _current;
    private TaskCompletionSource _settingsChanged = CreateSettingsChangedSignal();

    /// <summary>
    /// Initialises the state with the first settings revision.
    /// </summary>
    /// <param name="initialSettings">
    /// Settings used when the simulation starts.
    /// </param>
    public SimulationSettingsState(
        SimulationSettings initialSettings)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);
        _current = initialSettings;
    }

    /// <summary>
    /// Gets the current simulation settings.
    /// </summary>
    public SimulationSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Replaces the current settings with a newer revision.
    /// </summary>
    /// <param name="settings">
    /// New settings snapshot.
    /// </param>
    public void Update(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TaskCompletionSource settingsChanged;

        lock (_syncRoot)
        {
            if (settings.Revision <= _current.Revision)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    settings.Revision,
                    "The settings revision must be newer than the current revision.");
            }

            _current = settings;

            settingsChanged = _settingsChanged;
            _settingsChanged = CreateSettingsChangedSignal();
        }

        settingsChanged.SetResult();
    }

    /// <summary>
    /// Waits until settings newer than the observed revision are available.
    /// </summary>
    /// <param name="observedRevision">
    /// Revision currently used by the simulation.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the wait.
    /// </param>
    public Task WaitForChangeAsync(
        long observedRevision,
        CancellationToken cancellationToken)
    {
        Task settingsChangedTask;

        lock (_syncRoot)
        {
            if (_current.Revision != observedRevision)
            {
                return Task.CompletedTask;
            }

            settingsChangedTask = _settingsChanged.Task;
        }

        return settingsChangedTask.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a signal completed when the current settings are replaced.
    /// </summary>
    /// <returns>
    /// A new asynchronous settings change signal.
    /// </returns>
    private static TaskCompletionSource CreateSettingsChangedSignal()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
