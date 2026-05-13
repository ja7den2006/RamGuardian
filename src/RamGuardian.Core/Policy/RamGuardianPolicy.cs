using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Policy;

public static class RamGuardianPolicy
{
    public static CleanupPlan EvaluateAutoClean(
        MemorySnapshot snapshot,
        AutoCleanContext context,
        AutoCleanSettings? settings = null)
    {
        settings ??= AutoCleanSettings.Default;

        if (context.CleanupInProgress)
        {
            return CleanupPlan.None("Cleanup already in progress.");
        }

        if (context.LastCleanupAt is { } lastCleanupAt &&
            context.Now - lastCleanupAt < settings.Cooldown)
        {
            return CleanupPlan.None("Cleanup cooldown is active.");
        }

        var minimumAvailableBytes = Math.Max(
            settings.MinimumAvailableBytes,
            (ulong)Math.Round(snapshot.TotalPhysicalBytes * settings.MinimumAvailableRatio));

        var physicalPressure = snapshot.AvailablePhysicalBytes <= minimumAvailableBytes;
        var commitPressure = snapshot.UsedCommitRatio >= settings.CommitPressureRatio;
        var underPressure = snapshot.LowMemoryResourceSignaled || physicalPressure || commitPressure;

        if (!underPressure)
        {
            return CleanupPlan.None("Memory pressure is not present.");
        }

        if (!snapshot.LowMemoryResourceSignaled &&
            context.SustainedPressureDuration < settings.SustainedPressureWindow)
        {
            return CleanupPlan.None("Memory pressure has not lasted long enough.");
        }

        var criticalPressure =
            snapshot.LowMemoryResourceSignaled ||
            snapshot.AvailablePhysicalBytes <= settings.CriticalAvailableBytes ||
            snapshot.MemoryLoadPercent >= settings.CriticalMemoryLoadPercent;

        if (context.Foreground.IsFullscreenInteractive && !criticalPressure)
        {
            return new CleanupPlan(
                CleanupMode.AutoLowPriorityStandby,
                PurgeLowPriorityStandby: true,
                PurgeStandby: false,
                TrimBackgroundWorkingSets: false,
                TrimSystemWorkingSets: false,
                Reason: "Fullscreen workload detected, using the lightest auto-clean profile.");
        }

        return new CleanupPlan(
            CleanupMode.AutoStandby,
            PurgeLowPriorityStandby: false,
            PurgeStandby: true,
            TrimBackgroundWorkingSets: false,
            TrimSystemWorkingSets: false,
            Reason: criticalPressure
                ? "Critical memory pressure detected, using standard standby cleanup."
                : "Sustained memory pressure detected, using standard standby cleanup.");
    }

    public static CleanupPlan CreateManualCleanPlan(
        MemorySnapshot snapshot,
        ForegroundActivityContext foreground)
    {
        var severePressure =
            snapshot.LowMemoryResourceSignaled ||
            snapshot.MemoryLoadPercent >= 95 ||
            snapshot.AvailablePhysicalBytes <= 256UL * 1024UL * 1024UL;

        var trimSystemWorkingSets = severePressure && !foreground.IsFullscreenInteractive;

        return new CleanupPlan(
            CleanupMode.ManualBalanced,
            PurgeLowPriorityStandby: false,
            PurgeStandby: true,
            TrimBackgroundWorkingSets: true,
            TrimSystemWorkingSets: trimSystemWorkingSets,
            Reason: trimSystemWorkingSets
                ? "Manual clean escalated because memory pressure is severe."
                : "Manual clean will purge standby memory and trim background working sets.");
    }
}
