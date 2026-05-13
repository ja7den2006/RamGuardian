using Microsoft.Win32.SafeHandles;

namespace RamGuardian.Core.Interop;

internal sealed class SafeMemoryResourceNotificationHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeMemoryResourceNotificationHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
}
