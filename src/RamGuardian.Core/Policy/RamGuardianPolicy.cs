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
        var maintenancePressure = snapshot.MemoryLoadPercent >= settings.MaintenanceMemoryLoadPercent;
        var underPressure = snapshot.LowMemoryResourceSignaled || physicalPressure || commitPressure || maintenancePressure;

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
        var aggressiveAutoPressure =
            criticalPressure ||
            snapshot.MemoryLoadPercent >= settings.AggressiveAutoMemoryLoadPercent ||
            snapshot.AvailablePhysicalBytes <= minimumAvailableBytes;

        if (context.Foreground.IsFullscreenInteractive && !criticalPressure)
        {
            return new CleanupPlan(
                CleanupMode.AutoLowPriorityStandby,
                PurgeLowPriorityStandby: true,
                PurgeStandby: false,
                TrimBackgroundWorkingSets: false,
                TrimSystemWorkingSets: false,
                ExcludedProcessId: context.Foreground.ProcessId,
                Reason: "Fullscreen workload detected, using the lightest auto-clean profile.");
        }

        return new CleanupPlan(
            CleanupMode.AutoStandby,
            PurgeLowPriorityStandby: aggressiveAutoPressure,
            PurgeStandby: true,
            TrimBackgroundWorkingSets: aggressiveAutoPressure,
            TrimSystemWorkingSets: criticalPressure && !context.Foreground.IsFullscreenInteractive,
            ExcludedProcessId: context.Foreground.ProcessId,
            Reason: criticalPressure
                ? "Critical memory pressure detected, using aggressive auto-clean."
                : aggressiveAutoPressure
                    ? "Sustained memory pressure detected, using aggressive auto-clean."
                    : "Maintenance cleanup triggered, using standby cleanup.");
    }

    public static CleanupPlan CreateManualCleanPlan(
        MemorySnapshot snapshot,
        ForegroundActivityContext foreground)
    {
        var severePressure =
            snapshot.LowMemoryResourceSignaled ||
            snapshot.MemoryLoadPercent >= 95 ||
            snapshot.AvailablePhysicalBytes <= 256UL * 1024UL * 1024UL;

        var trimSystemWorkingSets = !foreground.IsFullscreenInteractive;

        return new CleanupPlan(
            CleanupMode.ManualBalanced,
            PurgeLowPriorityStandby: true,
            PurgeStandby: true,
            TrimBackgroundWorkingSets: true,
            TrimSystemWorkingSets: trimSystemWorkingSets,
            ExcludedProcessId: foreground.ProcessId,
            Reason: trimSystemWorkingSets
                ? severePressure
                    ? "Manual clean escalated because memory pressure is severe."
                    : "Manual clean will run an aggressive multi-pass trim."
                : "Manual clean will purge standby memory and trim background working sets.");
    }
}
