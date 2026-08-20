using DrillingTelemetry.Contracts;
using DrillingTelemetry.DeviceSimulator.Simulation;

namespace DrillingTelemetry.DeviceSimulator.Tests.Simulation;

/// <summary>
/// Tests the concurrent state used by the running simulation.
/// </summary>
public sealed class SimulationSettingsStateTests
{
    /// <summary>
    /// Verifies that an obsolete revision does not replace current settings.
    /// </summary>
    [Fact]
    public void TryUpdate_ObsoleteRevision_KeepsCurrentSettings()
    {
        // Arrange
        SimulationSettings initialSettings = CreateSettings(
            revision: 2,
            deviceId: "DRILL-002");

        var state = new SimulationSettingsState(initialSettings);

        SimulationSettings obsoleteSettings = CreateSettings(
            revision: 1,
            deviceId: "DRILL-001");

        // Act
        bool updated = state.TryUpdate(obsoleteSettings);

        // Assert
        Assert.False(updated);
        Assert.Same(initialSettings, state.Current);
    }

    /// <summary>
    /// Verifies that applying newer settings wakes an existing waiter.
    /// </summary>
    [Fact]
    public async Task WaitForChangeAsync_NewerRevisionApplied_CompletesWait()
    {
        // Arrange
        SimulationSettings initialSettings = CreateSettings(
            revision: 1,
            deviceId: "DRILL-001");

        var state = new SimulationSettingsState(initialSettings);

        Task waitTask = state.WaitForChangeAsync(
            initialSettings.Revision,
            CancellationToken.None);

        // Act
        bool updated = state.TryUpdate(
            CreateSettings(
                revision: 2,
                deviceId: "DRILL-002"));

        await waitTask;

        // Assert
        Assert.True(updated);
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Verifies that a change occurring before the wait is not missed.
    /// </summary>
    [Fact]
    public async Task WaitForChangeAsync_SettingsChangedBeforeWait_CompletesImmediately()
    {
        // Arrange
        SimulationSettings initialSettings = CreateSettings(
            revision: 1,
            deviceId: "DRILL-001");

        var state = new SimulationSettingsState(initialSettings);
        long observedRevision = state.Current.Revision;

        Assert.True(
            state.TryUpdate(
                CreateSettings(
                    revision: 2,
                    deviceId: "DRILL-002")));

        // Act
        Task waitTask = state.WaitForChangeAsync(
            observedRevision,
            CancellationToken.None);

        await waitTask;

        // Assert
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    /// <summary>
    /// Creates valid settings for state tests.
    /// </summary>
    /// <param name="revision">Settings revision.</param>
    /// <param name="deviceId">Configured device identifier.</param>
    /// <returns>A valid immutable settings snapshot.</returns>
    private static SimulationSettings CreateSettings(
        long revision,
        string deviceId)
    {
        return new SimulationSettings(
            revision,
            [deviceId],
            TimeSpan.FromSeconds(1),
            DrillingOperation.Stationary,
            depthChangeRateMetresPerHour: 0);
    }
}
