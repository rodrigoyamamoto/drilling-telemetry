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
    /// Applies a settings update command.
    /// </summary>
    /// <param name="command">
    /// Command containing the new runtime settings.
    /// </param>
    public void Apply(UpdateSimulationSettingsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = new SimulationSettings(
            command.Revision,
            command.DeviceIds,
            TimeSpan.FromMilliseconds(
                command.PublishingIntervalMilliseconds));

        _settingsState.Update(settings);
    }
}
