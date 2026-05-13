using System.ComponentModel;
using System.Runtime.InteropServices;

namespace RamGuardian.Core.Interop;

internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(nint windowHandle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(nint windowHandle, out int processId);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint windowHandle, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial SafeMemoryResourceNotificationHandle CreateMemoryResourceNotification(
        MemoryResourceNotificationType notificationType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryMemoryResourceNotification(
        SafeMemoryResourceNotificationHandle resourceNotificationHandle,
        [MarshalAs(UnmanagedType.Bool)] out bool resourceState);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(
        ProcessAccessRights desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyWorkingSet(nint processHandle);

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPerformanceInfo(
        out PerformanceInformation performanceInformation,
        int size);

    [LibraryImport("ntdll.dll")]
    public static partial int NtSetSystemInformation(
        SystemInformationClass systemInformationClass,
        ref SystemMemoryListCommand systemInformation,
        int systemInformationLength);

    public static void ThrowLastWin32Exception(string apiName)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), $"{apiName} failed.");
    }
}
