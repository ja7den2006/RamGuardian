using System.ComponentModel;
using System.Diagnostics;
using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Interop;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed class WindowsMemoryCleanupExecutor
{
    private const int ManualMinimumPasses = 5;
    private const int ManualMaximumPasses = 8;
    private const long ManualContinueThresholdBytes = 8L * 1024L * 1024L;
    private const ulong ManualContinueAvailableBytes = 2560UL * 1024UL * 1024UL;
    private const uint ManualContinueMemoryLoadPercent = 54;
    private const int AutoMinimumPasses = 2;
    private const int AutoMaximumPasses = 4;
    private const long AutoContinueThresholdBytes = 16L * 1024L * 1024L;
    private const ulong AutoContinueAvailableBytes = 2UL * 1024UL * 1024UL * 1024UL;
    private const uint AutoContinueMemoryLoadPercent = 62;
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss",
        "dwm",
        "explorer",
        "idle",
        "lsass",
        "services",
        "smss",
        "wininit",
        "winlogon",
    };

    private readonly WindowsMemoryTelemetryReader _telemetryReader;
    private readonly long _minimumProcessWorkingSetBytes;

    public WindowsMemoryCleanupExecutor(
        WindowsMemoryTelemetryReader telemetryReader,
        long minimumProcessWorkingSetBytes = 32L * 1024L * 1024L)
    {
        _telemetryReader = telemetryReader;
        _minimumProcessWorkingSetBytes = minimumProcessWorkingSetBytes;
    }

    public CleanupExecutionResult Execute(CleanupPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.Mode == CleanupMode.ManualBalanced)
        {
            return ExecuteMultiPassCleanupSequence(
                plan,
                minimumPasses: ManualMinimumPasses,
                maximumPasses: ManualMaximumPasses,
                continueThresholdBytes: ManualContinueThresholdBytes,
                continueAvailableBytes: ManualContinueAvailableBytes,
                continueMemoryLoadPercent: ManualContinueMemoryLoadPercent,
                settleDelay: TimeSpan.FromMilliseconds(320),
                retainSystemTrimPasses: 3,
                cancellationToken);
        }

        if (plan.Mode == CleanupMode.AutoStandby && plan.TrimBackgroundWorkingSets)
        {
            return ExecuteMultiPassCleanupSequence(
                plan,
                minimumPasses: AutoMinimumPasses,
                maximumPasses: AutoMaximumPasses,
                continueThresholdBytes: AutoContinueThresholdBytes,
                continueAvailableBytes: AutoContinueAvailableBytes,
                continueMemoryLoadPercent: AutoContinueMemoryLoadPercent,
                settleDelay: TimeSpan.FromMilliseconds(260),
                retainSystemTrimPasses: 1,
                cancellationToken);
        }

        return ExecuteSinglePass(plan, cancellationToken);
    }

    private CleanupExecutionResult ExecuteSinglePass(CleanupPlan plan, CancellationToken cancellationToken)
    {
        var before = _telemetryReader.CaptureSnapshot();

        if (plan.Mode == CleanupMode.None)
        {
            return new CleanupExecutionResult(
                Plan: plan,
                Before: before,
                After: before,
                ReclaimedPhysicalBytes: 0,
                TrimmedProcessCount: 0,
                Warnings: [],
                PassCount: 1);
        }

        var warnings = new List<string>();
        var trimmedProcessCount = 0;

        if (plan.PurgeLowPriorityStandby)
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList, warnings);
        }

        if (plan.PurgeStandby)
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryPurgeStandbyList, warnings);
        }

        if (plan.FlushModifiedList)
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryFlushModifiedList, warnings);
        }

        if (plan.TrimBackgroundWorkingSets)
        {
            trimmedProcessCount = TrimBackgroundWorkingSets(plan.ExcludedProcessId, cancellationToken, warnings);
        }

        if (plan.TrimSystemWorkingSets)
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryEmptyWorkingSets, warnings);
        }

        if (plan.PurgeLowPriorityStandby && (plan.TrimBackgroundWorkingSets || plan.TrimSystemWorkingSets))
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList, warnings);
        }

        if (plan.PurgeStandby && (plan.TrimBackgroundWorkingSets || plan.TrimSystemWorkingSets || plan.FlushModifiedList))
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryPurgeStandbyList, warnings);
        }

        var after = _telemetryReader.CaptureSnapshot();
        var reclaimedPhysicalBytes = unchecked((long)before.UsedPhysicalBytes - (long)after.UsedPhysicalBytes);

        return new CleanupExecutionResult(
            Plan: plan,
            Before: before,
            After: after,
            ReclaimedPhysicalBytes: reclaimedPhysicalBytes,
            TrimmedProcessCount: trimmedProcessCount,
            Warnings: warnings,
            PassCount: 1);
    }

    private CleanupExecutionResult ExecuteMultiPassCleanupSequence(
        CleanupPlan plan,
        int minimumPasses,
        int maximumPasses,
        long continueThresholdBytes,
        ulong continueAvailableBytes,
        uint continueMemoryLoadPercent,
        TimeSpan settleDelay,
        int retainSystemTrimPasses,
        CancellationToken cancellationToken)
    {
        CleanupExecutionResult? aggregate = null;

        for (var passIndex = 0; passIndex < maximumPasses; passIndex += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var passPlan = plan with
            {
                TrimSystemWorkingSets = plan.TrimSystemWorkingSets && passIndex < retainSystemTrimPasses,
            };

            var passResult = ExecuteSinglePass(passPlan, cancellationToken);
            aggregate = aggregate is null
                ? passResult
                : aggregate.Merge(passResult);

            var completedPasses = passIndex + 1;
            if (!ShouldContinueCleanup(
                    aggregate,
                    passResult,
                    completedPasses,
                    minimumPasses,
                    maximumPasses,
                    continueThresholdBytes,
                    continueAvailableBytes,
                    continueMemoryLoadPercent))
            {
                break;
            }

            Thread.Sleep(settleDelay);
        }

        return aggregate ?? ExecuteSinglePass(plan, cancellationToken);
    }

    private static bool ShouldContinueCleanup(
        CleanupExecutionResult aggregate,
        CleanupExecutionResult passResult,
        int completedPasses,
        int minimumPasses,
        int maximumPasses,
        long continueThresholdBytes,
        ulong continueAvailableBytes,
        uint continueMemoryLoadPercent)
    {
        if (completedPasses < minimumPasses)
        {
            return true;
        }

        if (completedPasses >= maximumPasses)
        {
            return false;
        }

        if (passResult.ReclaimedPhysicalBytes >= continueThresholdBytes)
        {
            return true;
        }

        return aggregate.After.MemoryLoadPercent >= continueMemoryLoadPercent ||
               aggregate.After.AvailablePhysicalBytes <= continueAvailableBytes;
    }

    private static void TryApplyMemoryListCommand(SystemMemoryListCommand command, ICollection<string> warnings)
    {
        var status = NativeMethods.NtSetSystemInformation(
            SystemInformationClass.SystemMemoryListInformation,
            ref command,
            sizeof(int));

        if (status != 0)
        {
            warnings.Add($"{command} returned NTSTATUS 0x{status:X8}.");
        }
    }

    private int TrimBackgroundWorkingSets(int? excludedProcessId, CancellationToken cancellationToken, ICollection<string> warnings)
    {
        using var currentProcess = Process.GetCurrentProcess();
        var currentProcessId = currentProcess.Id;
        var currentSessionId = currentProcess.SessionId;
        var trimmed = 0;

        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (process)
            {
                if (!ShouldTrim(process, currentProcessId, currentSessionId, excludedProcessId))
                {
                    continue;
                }

                var handle = NativeMethods.OpenProcess(
                    ProcessAccessRights.QueryLimitedInformation | ProcessAccessRights.SetQuota,
                    inheritHandle: false,
                    process.Id);

                if (handle == 0)
                {
                    continue;
                }

                try
                {
                    if (NativeMethods.EmptyWorkingSet(handle))
                    {
                        trimmed += 1;
                    }
                }
                catch (Win32Exception ex)
                {
                    warnings.Add($"{process.ProcessName}: {ex.Message}");
                }
                finally
                {
                    NativeMethods.CloseHandle(handle);
                }
            }
        }

        return trimmed;
    }

    private bool ShouldTrim(Process process, int currentProcessId, int currentSessionId, int? excludedProcessId)
    {
        try
        {
            if (process.Id == currentProcessId || process.Id <= 4)
            {
                return false;
            }

            if (excludedProcessId.HasValue && process.Id == excludedProcessId.Value)
            {
                return false;
            }

            if (process.SessionId != currentSessionId)
            {
                return false;
            }

            if (ProtectedProcessNames.Contains(process.ProcessName))
            {
                return false;
            }

            if (process.WorkingSet64 < _minimumProcessWorkingSetBytes)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
