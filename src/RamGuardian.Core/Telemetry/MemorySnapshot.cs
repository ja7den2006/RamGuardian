namespace RamGuardian.Core.Telemetry;

public sealed record MemorySnapshot(
    DateTimeOffset CapturedAt,
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes,
    ulong TotalCommitBytes,
    ulong AvailableCommitBytes,
    uint MemoryLoadPercent,
    bool LowMemoryResourceSignaled)
{
    public ulong UsedPhysicalBytes =>
        TotalPhysicalBytes > AvailablePhysicalBytes
            ? TotalPhysicalBytes - AvailablePhysicalBytes
            : 0;

    public ulong UsedCommitBytes =>
        TotalCommitBytes > AvailableCommitBytes
            ? TotalCommitBytes - AvailableCommitBytes
            : 0;

    public double UsedCommitRatio =>
        TotalCommitBytes == 0
            ? 0d
            : (double)UsedCommitBytes / TotalCommitBytes;
}
