using RamGuardian.Core.Interop;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed class WindowsMemoryTelemetryReader : IDisposable
{
    private readonly SafeMemoryResourceNotificationHandle _lowMemoryHandle;
    private bool _disposed;

    public WindowsMemoryTelemetryReader()
    {
        _lowMemoryHandle = NativeMethods.CreateMemoryResourceNotification(
            MemoryResourceNotificationType.LowMemoryResourceNotification);

        if (_lowMemoryHandle.IsInvalid)
        {
            NativeMethods.ThrowLastWin32Exception(nameof(NativeMethods.CreateMemoryResourceNotification));
        }
    }

    public MemorySnapshot CaptureSnapshot()
    {
        ThrowIfDisposed();

        var memoryStatus = new MemoryStatusEx
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MemoryStatusEx>(),
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref memoryStatus))
        {
            NativeMethods.ThrowLastWin32Exception(nameof(NativeMethods.GlobalMemoryStatusEx));
        }

        if (!NativeMethods.GetPerformanceInfo(
                out var performanceInformation,
                System.Runtime.InteropServices.Marshal.SizeOf<PerformanceInformation>()))
        {
            NativeMethods.ThrowLastWin32Exception(nameof(NativeMethods.GetPerformanceInfo));
        }

        if (!NativeMethods.QueryMemoryResourceNotification(_lowMemoryHandle, out var lowMemorySignaled))
        {
            NativeMethods.ThrowLastWin32Exception(nameof(NativeMethods.QueryMemoryResourceNotification));
        }

        var totalCommitBytes = checked((ulong)performanceInformation.CommitLimit * (ulong)performanceInformation.PageSize);
        var availableCommitPages = performanceInformation.CommitLimit - performanceInformation.CommitTotal;
        var availableCommitBytes = checked((ulong)availableCommitPages * (ulong)performanceInformation.PageSize);

        return new MemorySnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            TotalPhysicalBytes: memoryStatus.ullTotalPhys,
            AvailablePhysicalBytes: memoryStatus.ullAvailPhys,
            TotalCommitBytes: totalCommitBytes,
            AvailableCommitBytes: availableCommitBytes,
            MemoryLoadPercent: memoryStatus.dwMemoryLoad,
            LowMemoryResourceSignaled: lowMemorySignaled);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lowMemoryHandle.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
