using System.Diagnostics;
using RamGuardian.Core.Interop;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed class WindowsForegroundActivityDetector
{
    private const uint MonitorDefaultToNearest = 2;
    private const int FullscreenTolerancePixels = 2;

    public ForegroundActivityContext Detect()
    {
        try
        {
            var windowHandle = NativeMethods.GetForegroundWindow();

            if (windowHandle == 0 || !NativeMethods.IsWindowVisible(windowHandle))
            {
                return new ForegroundActivityContext(false);
            }

            _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);

            if (processId <= 0)
            {
                return new ForegroundActivityContext(false);
            }

            if (!NativeMethods.GetWindowRect(windowHandle, out var windowRect))
            {
                return new ForegroundActivityContext(false);
            }

            var monitorHandle = NativeMethods.MonitorFromWindow(windowHandle, MonitorDefaultToNearest);

            if (monitorHandle == 0)
            {
                return CreateContext(processId, isFullscreen: false);
            }

            var monitorInfo = new MonitorInfo
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>(),
            };

            if (!NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo))
            {
                return CreateContext(processId, isFullscreen: false);
            }

            var isFullscreen =
                Math.Abs(windowRect.Left - monitorInfo.rcMonitor.Left) <= FullscreenTolerancePixels &&
                Math.Abs(windowRect.Top - monitorInfo.rcMonitor.Top) <= FullscreenTolerancePixels &&
                Math.Abs(windowRect.Right - monitorInfo.rcMonitor.Right) <= FullscreenTolerancePixels &&
                Math.Abs(windowRect.Bottom - monitorInfo.rcMonitor.Bottom) <= FullscreenTolerancePixels &&
                windowRect.Width > 0 &&
                windowRect.Height > 0;

            return CreateContext(processId, isFullscreen);
        }
        catch
        {
            return new ForegroundActivityContext(false);
        }
    }

    private static ForegroundActivityContext CreateContext(int processId, bool isFullscreen)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return new ForegroundActivityContext(isFullscreen, process.ProcessName, processId);
        }
        catch
        {
            return new ForegroundActivityContext(isFullscreen);
        }
    }
}
