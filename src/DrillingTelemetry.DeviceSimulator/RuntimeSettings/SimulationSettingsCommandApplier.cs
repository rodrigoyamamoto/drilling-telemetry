using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.DeviceSimulator.Simulation;

namespace DrillingTelemetry.DeviceSimulator.RuntimeSettings;

/// <summary>
/// Applies settings commands to the running simulation.
/// </summary>
internal sealed class SimulationSettingsCommandApplier
{
    private readonly SimulationSettingsState _settingsState;

    /// <summary>
    /// Initialises a simulation settings command applier.
    /// </summary>
    /// <param name="settingsState">
    /// State containing the current runtime settings.
    /// </param>
    public SimulationSettingsCommandApplier(
        SimulationSettingsState settingsState)
    {
        ArgumentNullException.ThrowIfNull(settingsState);
        _settingsState = settingsState;
    }

    /// <summary>
    /// Attempts to apply a settings update command.
    /// </summary>
    /// <param name="command">
    /// Command containing the new runtime settings.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the command updated the settings;
    /// otherwise, <see langword="false"/> when its revision was obsolete.
    /// </returns>
    public bool TryApply(UpdateSimulationSettingsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = new SimulationSettings(
            command.Revision,
            command.DeviceIds,
            TimeSpan.FromMilliseconds(
                command.PublishingIntervalMilliseconds));

        return _settingsState.TryUpdate(settings);
    }
}
