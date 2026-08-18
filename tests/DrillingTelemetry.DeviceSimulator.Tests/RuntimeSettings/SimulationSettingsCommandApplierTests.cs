using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.DeviceSimulator.RuntimeSettings;
using DrillingTelemetry.DeviceSimulator.Simulation;

namespace DrillingTelemetry.DeviceSimulator.Tests.RuntimeSettings;

/// <summary>
/// Tests the application of runtime simulation settings commands.
/// </summary>
public sealed class SimulationSettingsCommandApplierTests
{
    /// <summary>
    /// Verifies that a valid command updates the current simulation settings.
    /// </summary>
    [Fact]
    public void TryApply_ValidCommand_UpdatesSimulationSettings()
    {
        // Arrange
        string[] initialDeviceIds =
        [
            "DRILL-001"
        ];

        string[] updatedDeviceIds =
        [
            "DRILL-001",
            "DRILL-002",
            "DRILL-003"
        ];

        var initialSettings = new SimulationSettings(
            revision: 1,
            deviceIds: initialDeviceIds,
            publishingInterval: TimeSpan.FromSeconds(2));

        var settingsState =
            new SimulationSettingsState(initialSettings);

        var applier =
            new SimulationSettingsCommandApplier(settingsState);

        var command = new UpdateSimulationSettingsCommand
        {
            Revision = 2,
            DeviceIds = updatedDeviceIds,
            PublishingIntervalMilliseconds = 500
        };

        // Act
        bool applied = applier.TryApply(command);

        // Assert
        Assert.True(applied);

        SimulationSettings currentSettings =
            settingsState.Current;

        Assert.Equal(
            command.Revision,
            currentSettings.Revision);

        Assert.Equal(
            updatedDeviceIds,
            currentSettings.DeviceIds);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                command.PublishingIntervalMilliseconds),
            currentSettings.PublishingInterval);
    }
}
