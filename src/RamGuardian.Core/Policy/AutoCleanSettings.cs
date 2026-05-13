namespace RamGuardian.Core.Policy;

public sealed record AutoCleanSettings
{
    public static AutoCleanSettings Default { get; } = new();

    public TimeSpan SustainedPressureWindow { get; init; } = TimeSpan.FromSeconds(3);

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(45);

    public double MinimumAvailableRatio { get; init; } = 0.12d;

    public ulong MinimumAvailableBytes { get; init; } = 1024UL * 1024UL * 1024UL;

    public ulong CriticalAvailableBytes { get; init; } = 640UL * 1024UL * 1024UL;

    public double CommitPressureRatio { get; init; } = 0.86d;

    public uint CriticalMemoryLoadPercent { get; init; } = 84;

    public uint MaintenanceMemoryLoadPercent { get; init; } = 55;

    public uint AggressiveAutoMemoryLoadPercent { get; init; } = 68;
}
