using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed record CleanupExecutionResult(
    CleanupPlan Plan,
    MemorySnapshot Before,
    MemorySnapshot After,
    long ReclaimedPhysicalBytes,
    int TrimmedProcessCount,
    IReadOnlyList<string> Warnings,
    int PassCount = 1)
{
    public CleanupExecutionResult Merge(CleanupExecutionResult next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return this with
        {
            After = next.After,
            ReclaimedPhysicalBytes = unchecked((long)Before.UsedPhysicalBytes - (long)next.After.UsedPhysicalBytes),
            TrimmedProcessCount = TrimmedProcessCount + next.TrimmedProcessCount,
            Warnings = [.. Warnings, .. next.Warnings],
            PassCount = PassCount + next.PassCount,
        };
    }
}
