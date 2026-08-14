namespace DrillingTelemetry.DeviceSimulator.Simulation;

/// <summary>
/// Stores and signals changes to the current simulation settings.
/// </summary>
internal sealed class SimulationSettingsStore
{
    private readonly object _syncRoot = new();

    private SimulationSettings _current;
    private TaskCompletionSource _settingsChanged = CreateSettingsChangedSignal();

    /// <summary>
    /// Initializes the store with the first configuration version.
    /// </summary>
    /// <param name="initialSettings">
    /// Configuration used when the simulation starts.
    /// </param>
    public SimulationSettingsStore(
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
    /// Replaces the current settings with a newer version.
    /// </summary>
    /// <param name="settings">
    /// New configuration snapshot.
    /// </param>
    public void Update(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        TaskCompletionSource settingsChanged;

        lock (_syncRoot)
        {
            if (settings.Version <= _current.Version)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    settings.Version,
                    "The settings version must be newer than the current version.");
            }

            _current = settings;

            settingsChanged = _settingsChanged;
            _settingsChanged = CreateSettingsChangedSignal();
        }

        settingsChanged.SetResult();
    }

    /// <summary>
    /// Waits until settings newer than the observed version are available.
    /// </summary>
    /// <param name="observedVersion">
    /// Version currently used by the simulation.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the wait.
    /// </param>
    public Task WaitForChangeAsync(
        long observedVersion,
        CancellationToken cancellationToken)
    {
        Task settingsChangedTask;

        lock (_syncRoot)
        {
            if (_current.Version != observedVersion)
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
