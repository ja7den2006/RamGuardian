namespace RamGuardian.App;

public sealed record AppState(bool AutoCleanEnabled)
{
    public static AppState Default { get; } = new(AutoCleanEnabled: false);
}
