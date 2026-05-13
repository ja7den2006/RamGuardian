using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed record CleanupExecutionResult(
    CleanupPlan Plan,
    MemorySnapshot Before,
    MemorySnapshot After,
    long ReclaimedPhysicalBytes,
    int TrimmedProcessCount,
    IReadOnlyList<string> Warnings);
