namespace RamGuardian.Core.Interop;

internal enum MemoryResourceNotificationType : uint
{
    LowMemoryResourceNotification = 0,
    HighMemoryResourceNotification = 1,
}

internal enum SystemInformationClass : int
{
    SystemMemoryListInformation = 80,
}

internal enum SystemMemoryListCommand : int
{
    MemoryCaptureAccessedBits = 0,
    MemoryCaptureAndResetAccessedBits = 1,
    MemoryEmptyWorkingSets = 2,
    MemoryFlushModifiedList = 3,
    MemoryPurgeStandbyList = 4,
    MemoryPurgeLowPriorityStandbyList = 5,
}

[Flags]
internal enum ProcessAccessRights : uint
{
    QueryInformation = 0x0400,
    QueryLimitedInformation = 0x1000,
    SetQuota = 0x0100,
}
