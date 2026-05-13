using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Engine;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Tests;

public sealed class CleanupExecutionResultTests
{
    [Fact]
    public void Merge_AccumulatesPassesWarningsAndTrimCount()
    {
        var first = CreateResult(
            beforeUsedPhysicalBytes: 10UL * 1024UL * 1024UL * 1024UL,
            afterUsedPhysicalBytes: 9UL * 1024UL * 1024UL * 1024UL,
            trimmedProcessCount: 5,
            warnings: ["first warning"]);

        var second = CreateResult(
            beforeUsedPhysicalBytes: 9UL * 1024UL * 1024UL * 1024UL,
            afterUsedPhysicalBytes: 8UL * 1024UL * 1024UL * 1024UL,
            trimmedProcessCount: 3,
            warnings: ["second warning"]);

        var merged = first.Merge(second);

        Assert.Equal(2, merged.PassCount);
        Assert.Equal(8, merged.TrimmedProcessCount);
        Assert.Equal(2UL * 1024UL * 1024UL * 1024UL, (ulong)merged.ReclaimedPhysicalBytes);
        Assert.Equal(2, merged.Warnings.Count);
    }

    private static CleanupExecutionResult CreateResult(
        ulong beforeUsedPhysicalBytes,
        ulong afterUsedPhysicalBytes,
        int trimmedProcessCount,
        IReadOnlyList<string> warnings)
    {
        const ulong totalPhysicalBytes = 16UL * 1024UL * 1024UL * 1024UL;
        const ulong totalCommitBytes = 32UL * 1024UL * 1024UL * 1024UL;
        const ulong availableCommitBytes = 4UL * 1024UL * 1024UL * 1024UL;
        var now = DateTimeOffset.UtcNow;

        var before = new MemorySnapshot(
            CapturedAt: now,
            TotalPhysicalBytes: totalPhysicalBytes,
            AvailablePhysicalBytes: totalPhysicalBytes - beforeUsedPhysicalBytes,
            TotalCommitBytes: totalCommitBytes,
            AvailableCommitBytes: availableCommitBytes,
            MemoryLoadPercent: 70,
            LowMemoryResourceSignaled: false);

        var after = new MemorySnapshot(
            CapturedAt: now.AddMilliseconds(500),
            TotalPhysicalBytes: totalPhysicalBytes,
            AvailablePhysicalBytes: totalPhysicalBytes - afterUsedPhysicalBytes,
            TotalCommitBytes: totalCommitBytes,
            AvailableCommitBytes: availableCommitBytes,
            MemoryLoadPercent: 60,
            LowMemoryResourceSignaled: false);

        return new CleanupExecutionResult(
            Plan: CleanupPlan.None("test"),
            Before: before,
            After: after,
            ReclaimedPhysicalBytes: unchecked((long)before.UsedPhysicalBytes - (long)after.UsedPhysicalBytes),
            TrimmedProcessCount: trimmedProcessCount,
            Warnings: warnings,
            PassCount: 1);
    }
}
