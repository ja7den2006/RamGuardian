namespace RamGuardian.Core.Policy;

public sealed record AutoCleanSettings
{
    public static AutoCleanSettings Default { get; } = new();

    public TimeSpan SustainedPressureWindow { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMinutes(2);

    public double MinimumAvailableRatio { get; init; } = 0.08d;

    public ulong MinimumAvailableBytes { get; init; } = 768UL * 1024UL * 1024UL;

    public ulong CriticalAvailableBytes { get; init; } = 384UL * 1024UL * 1024UL;

    public double CommitPressureRatio { get; init; } = 0.90d;

    public uint CriticalMemoryLoadPercent { get; init; } = 92;
}
