using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.DeviceSimulator.Simulation;

namespace DrillingTelemetry.DeviceSimulator.Control;

/// <summary>
/// Applies settings commands to the running simulation.
/// </summary>
internal sealed class SimulationSettingsCommandHandler
{
    private readonly SimulationSettingsStore _settingsStore;

    /// <summary>
    /// Initialises a simulation settings command handler.
    /// </summary>
    /// <param name="settingsStore">
    /// Store containing the current runtime settings.
    /// </param>
    public SimulationSettingsCommandHandler(
        SimulationSettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        _settingsStore = settingsStore;
    }

    /// <summary>
    /// Applies a settings update command.
    /// </summary>
    /// <param name="command">
    /// Command containing the new runtime settings.
    /// </param>
    public void Handle(UpdateSimulationSettingsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settings = new SimulationSettings(
            command.Version,
            command.DeviceIds,
            TimeSpan.FromMilliseconds(
                command.PublishingIntervalMilliseconds));

        _settingsStore.Update(settings);
    }
}
