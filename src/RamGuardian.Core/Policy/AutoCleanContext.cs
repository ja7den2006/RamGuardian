using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Policy;

public sealed record AutoCleanContext(
    DateTimeOffset Now,
    TimeSpan SustainedPressureDuration,
    DateTimeOffset? LastCleanupAt,
    ForegroundActivityContext Foreground,
    bool CleanupInProgress = false);
