using System.IO;

namespace RamGuardian.App;

public sealed class ActivityLogger
{
    private readonly string _logPath;
    private readonly object _sync = new();

    public ActivityLogger(string? logPath = null)
    {
        _logPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RamGuardian",
            "activity.log");
    }

    public void Write(string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                File.AppendAllText(_logPath, line);
            }
        }
        catch
        {
            // Logging must never destabilize the app.
        }
    }
}
