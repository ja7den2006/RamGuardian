using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Engine;
using RamGuardian.Core.Policy;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Tests;

public sealed class RamGuardianPolicyTests
{
    [Fact]
    public void EvaluateAutoClean_ReturnsNone_WhenCooldownIsActive()
    {
        var snapshot = CreateSnapshot(
            availablePhysicalBytes: 512UL * 1024UL * 1024UL,
            memoryLoadPercent: 88,
            lowMemoryResourceSignaled: true);

        var context = new AutoCleanContext(
            Now: snapshot.CapturedAt,
            SustainedPressureDuration: TimeSpan.FromSeconds(10),
            LastCleanupAt: snapshot.CapturedAt - TimeSpan.FromSeconds(30),
            Foreground: new ForegroundActivityContext(false));

        var plan = RamGuardianPolicy.EvaluateAutoClean(snapshot, context);

        Assert.Equal(CleanupMode.None, plan.Mode);
        Assert.Contains("cooldown", plan.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateAutoClean_UsesLowPriorityStandby_ForFullscreenPressure()
    {
        var snapshot = CreateSnapshot(
            availablePhysicalBytes: 700UL * 1024UL * 1024UL,
            memoryLoadPercent: 84,
            lowMemoryResourceSignaled: false);

        var context = new AutoCleanContext(
            Now: snapshot.CapturedAt,
            SustainedPressureDuration: TimeSpan.FromSeconds(8),
            LastCleanupAt: null,
            Foreground: new ForegroundActivityContext(true, "game.exe"));

        var plan = RamGuardianPolicy.EvaluateAutoClean(snapshot, context);

        Assert.Equal(CleanupMode.AutoLowPriorityStandby, plan.Mode);
        Assert.True(plan.PurgeLowPriorityStandby);
        Assert.False(plan.PurgeStandby);
        Assert.False(plan.TrimBackgroundWorkingSets);
    }

    [Fact]
    public void EvaluateAutoClean_UsesStandardStandby_WhenPressureIsCritical()
    {
        var snapshot = CreateSnapshot(
            availablePhysicalBytes: 300UL * 1024UL * 1024UL,
            memoryLoadPercent: 95,
            lowMemoryResourceSignaled: false);

        var context = new AutoCleanContext(
            Now: snapshot.CapturedAt,
            SustainedPressureDuration: TimeSpan.FromSeconds(8),
            LastCleanupAt: null,
            Foreground: new ForegroundActivityContext(true, "game.exe"));

        var plan = RamGuardianPolicy.EvaluateAutoClean(snapshot, context);

        Assert.Equal(CleanupMode.AutoStandby, plan.Mode);
        Assert.True(plan.PurgeStandby);
        Assert.False(plan.PurgeLowPriorityStandby);
    }

    [Fact]
    public void CreateManualCleanPlan_AvoidsSystemWideTrim_DuringFullscreenUse()
    {
        var snapshot = CreateSnapshot(
            availablePhysicalBytes: 200UL * 1024UL * 1024UL,
            memoryLoadPercent: 96,
            lowMemoryResourceSignaled: true);

        var plan = RamGuardianPolicy.CreateManualCleanPlan(
            snapshot,
            new ForegroundActivityContext(true, "game.exe"));

        Assert.Equal(CleanupMode.ManualBalanced, plan.Mode);
        Assert.True(plan.PurgeStandby);
        Assert.True(plan.TrimBackgroundWorkingSets);
        Assert.False(plan.TrimSystemWorkingSets);
    }

    [Fact]
    public void CreateManualCleanPlan_AllowsSystemWideTrim_WhenPressureIsSevereAndNotFullscreen()
    {
        var snapshot = CreateSnapshot(
            availablePhysicalBytes: 200UL * 1024UL * 1024UL,
            memoryLoadPercent: 96,
            lowMemoryResourceSignaled: true);

        var plan = RamGuardianPolicy.CreateManualCleanPlan(
            snapshot,
            new ForegroundActivityContext(false, "explorer.exe"));

        Assert.True(plan.TrimSystemWorkingSets);
    }

    [Fact]
    public void WindowsForegroundActivityDetector_Detect_DoesNotThrow()
    {
        var detector = new WindowsForegroundActivityDetector();
        var exception = Record.Exception(() => detector.Detect());

        Assert.Null(exception);
    }

    private static MemorySnapshot CreateSnapshot(
        ulong availablePhysicalBytes,
        uint memoryLoadPercent,
        bool lowMemoryResourceSignaled)
    {
        const ulong totalPhysicalBytes = 16UL * 1024UL * 1024UL * 1024UL;
        const ulong totalCommitBytes = 32UL * 1024UL * 1024UL * 1024UL;
        const ulong availableCommitBytes = 2UL * 1024UL * 1024UL * 1024UL;

        return new MemorySnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            TotalPhysicalBytes: totalPhysicalBytes,
            AvailablePhysicalBytes: availablePhysicalBytes,
            TotalCommitBytes: totalCommitBytes,
            AvailableCommitBytes: availableCommitBytes,
            MemoryLoadPercent: memoryLoadPercent,
            LowMemoryResourceSignaled: lowMemoryResourceSignaled);
    }
}
