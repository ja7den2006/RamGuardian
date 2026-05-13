namespace RamGuardian.Core.Telemetry;

public sealed record ForegroundActivityContext(
    bool IsFullscreenInteractive,
    string? ProcessName = null,
    int? ProcessId = null);
