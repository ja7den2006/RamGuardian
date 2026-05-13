namespace RamGuardian.Core.Cleaning;

public sealed record CleanupPlan(
    CleanupMode Mode,
    bool PurgeLowPriorityStandby,
    bool PurgeStandby,
    bool TrimBackgroundWorkingSets,
    bool TrimSystemWorkingSets,
    string Reason,
    int? ExcludedProcessId = null)
{
    public static CleanupPlan None(string reason) =>
        new(
            CleanupMode.None,
            PurgeLowPriorityStandby: false,
            PurgeStandby: false,
            TrimBackgroundWorkingSets: false,
            TrimSystemWorkingSets: false,
            Reason: reason);
}
