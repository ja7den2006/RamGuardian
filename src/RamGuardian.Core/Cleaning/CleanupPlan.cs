namespace RamGuardian.Core.Cleaning;

public sealed record CleanupPlan(
    CleanupMode Mode,
    bool PurgeLowPriorityStandby,
    bool PurgeStandby,
    bool FlushModifiedList,
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
            FlushModifiedList: false,
            TrimBackgroundWorkingSets: false,
            TrimSystemWorkingSets: false,
            Reason: reason);
}
