using DrillingTelemetry.Contracts.Commands;
using DrillingTelemetry.DeviceSimulator.Control;
using DrillingTelemetry.DeviceSimulator.Simulation;

namespace DrillingTelemetry.DeviceSimulator.Tests.Control;

/// <summary>
/// Tests the application of runtime simulation settings commands.
/// </summary>
public sealed class SimulationSettingsCommandHandlerTests
{
    /// <summary>
    /// Verifies that a valid command updates the current simulation settings.
    /// </summary>
    [Fact]
    public void Handle_ValidCommand_UpdatesSimulationSettings()
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
            version: 1,
            deviceIds: initialDeviceIds,
            publishingInterval: TimeSpan.FromSeconds(2));

        var settingsStore =
            new SimulationSettingsStore(initialSettings);

        var handler =
            new SimulationSettingsCommandHandler(settingsStore);

        var command = new UpdateSimulationSettingsCommand
        {
            Version = 2,
            DeviceIds = updatedDeviceIds,
            PublishingIntervalMilliseconds = 500
        };

        // Act
        handler.Handle(command);

        // Assert
        SimulationSettings currentSettings =
            settingsStore.Current;

        Assert.Equal(
            command.Version,
            currentSettings.Version);

        Assert.Equal(
            updatedDeviceIds,
            currentSettings.DeviceIds);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                command.PublishingIntervalMilliseconds),
            currentSettings.PublishingInterval);
    }
}
