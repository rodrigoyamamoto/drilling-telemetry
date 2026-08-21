using DrillingTelemetry.Processor.Processing;

namespace DrillingTelemetry.Processor.Tests.Processing;

/// <summary>
/// Verifies concurrent acquisition ownership detection and suppression.
/// </summary>
public sealed class ConcurrentAcquisitionSessionDetectorTests
{
    private static readonly Guid FirstSessionId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly Guid SecondSessionId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// Verifies that one continuously active conflicting pair emits only
    /// one warning, even after the activity window has elapsed.
    /// </summary>
    [Fact]
    public void Observe_ContinuouslyActiveConflict_EmitsOnce()
    {
        // Arrange
        var timeProvider = new AdjustableTimeProvider();
        var detector = new ConcurrentAcquisitionSessionDetector(
            timeProvider,
            TimeSpan.FromSeconds(30));

        // Act
        ConcurrentAcquisitionSessionConflict? baseline = detector.Observe(
            "DRILL-001",
            FirstSessionId);
        ConcurrentAcquisitionSessionConflict? conflict = detector.Observe(
            "DRILL-001",
            SecondSessionId);

        timeProvider.Advance(TimeSpan.FromSeconds(20));
        ConcurrentAcquisitionSessionConflict? repeatedFirst = detector.Observe(
            "DRILL-001",
            FirstSessionId);
        ConcurrentAcquisitionSessionConflict? repeatedSecond = detector.Observe(
            "DRILL-001",
            SecondSessionId);

        timeProvider.Advance(TimeSpan.FromSeconds(20));
        ConcurrentAcquisitionSessionConflict? afterWindow = detector.Observe(
            "DRILL-001",
            FirstSessionId);

        // Assert
        Assert.Null(baseline);
        Assert.NotNull(conflict);
        Assert.Equal(FirstSessionId, conflict.ExistingSessionId);
        Assert.Equal(SecondSessionId, conflict.ObservedSessionId);
        Assert.Null(repeatedFirst);
        Assert.Null(repeatedSecond);
        Assert.Null(afterWindow);
    }

    /// <summary>
    /// Verifies that the same conflict may be reported again after both
    /// sessions cease activity and a new overlap begins.
    /// </summary>
    [Fact]
    public void Observe_ConflictAfterInactivity_EmitsAgain()
    {
        // Arrange
        var timeProvider = new AdjustableTimeProvider();
        var detector = new ConcurrentAcquisitionSessionDetector(
            timeProvider,
            TimeSpan.FromSeconds(30));

        detector.Observe("DRILL-001", FirstSessionId);
        ConcurrentAcquisitionSessionConflict? initialConflict =
            detector.Observe("DRILL-001", SecondSessionId);

        // Act
        timeProvider.Advance(TimeSpan.FromSeconds(31));
        ConcurrentAcquisitionSessionConflict? restartedBaseline =
            detector.Observe("DRILL-001", FirstSessionId);
        ConcurrentAcquisitionSessionConflict? repeatedConflict =
            detector.Observe("DRILL-001", SecondSessionId);

        // Assert
        Assert.NotNull(initialConflict);
        Assert.Null(restartedBaseline);
        Assert.NotNull(repeatedConflict);
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        /// <summary>Advances the current test time.</summary>
        /// <param name="duration">Amount of time to advance.</param>
        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
