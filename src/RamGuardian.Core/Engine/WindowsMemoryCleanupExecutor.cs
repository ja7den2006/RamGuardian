using System.ComponentModel;
using System.Diagnostics;
using RamGuardian.Core.Cleaning;
using RamGuardian.Core.Interop;
using RamGuardian.Core.Telemetry;

namespace RamGuardian.Core.Engine;

public sealed class WindowsMemoryCleanupExecutor
{
    private const int ManualMinimumPasses = 2;
    private const int ManualMaximumPasses = 4;
    private const long ManualContinueThresholdBytes = 48L * 1024L * 1024L;
    private const ulong ManualContinueAvailableBytes = 1024UL * 1024UL * 1024UL;
    private const uint ManualContinueMemoryLoadPercent = 82;
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
        long minimumProcessWorkingSetBytes = 64L * 1024L * 1024L)
    {
        _telemetryReader = telemetryReader;
        _minimumProcessWorkingSetBytes = minimumProcessWorkingSetBytes;
    }

    public CleanupExecutionResult Execute(CleanupPlan plan, CancellationToken cancellationToken = default)
    {
        return plan.Mode == CleanupMode.ManualBalanced
            ? ExecuteManualCleanupSequence(plan, cancellationToken)
            : ExecuteSinglePass(plan, cancellationToken);
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

        if (plan.TrimBackgroundWorkingSets)
        {
            trimmedProcessCount = TrimBackgroundWorkingSets(cancellationToken, warnings);
        }

        if (plan.TrimSystemWorkingSets)
        {
            TryApplyMemoryListCommand(SystemMemoryListCommand.MemoryEmptyWorkingSets, warnings);
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

    private CleanupExecutionResult ExecuteManualCleanupSequence(CleanupPlan plan, CancellationToken cancellationToken)
    {
        CleanupExecutionResult? aggregate = null;

        for (var passIndex = 0; passIndex < ManualMaximumPasses; passIndex += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var passPlan = passIndex == 0
                ? plan
                : plan with { TrimSystemWorkingSets = false };

            var passResult = ExecuteSinglePass(passPlan, cancellationToken);
            aggregate = aggregate is null
                ? passResult
                : aggregate.Merge(passResult);

            var completedPasses = passIndex + 1;
            if (!ShouldContinueManualCleanup(aggregate, passResult, completedPasses))
            {
                break;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(325));
        }

        return aggregate ?? ExecuteSinglePass(plan, cancellationToken);
    }

    private static bool ShouldContinueManualCleanup(
        CleanupExecutionResult aggregate,
        CleanupExecutionResult passResult,
        int completedPasses)
    {
        if (completedPasses < ManualMinimumPasses)
        {
            return true;
        }

        if (completedPasses >= ManualMaximumPasses)
        {
            return false;
        }

        if (passResult.ReclaimedPhysicalBytes >= ManualContinueThresholdBytes)
        {
            return true;
        }

        return aggregate.After.MemoryLoadPercent >= ManualContinueMemoryLoadPercent ||
               aggregate.After.AvailablePhysicalBytes <= ManualContinueAvailableBytes;
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

    private int TrimBackgroundWorkingSets(CancellationToken cancellationToken, ICollection<string> warnings)
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
                if (!ShouldTrim(process, currentProcessId, currentSessionId))
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

    private bool ShouldTrim(Process process, int currentProcessId, int currentSessionId)
    {
        try
        {
            if (process.Id == currentProcessId || process.Id <= 4)
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

            if (process.MainWindowHandle != nint.Zero)
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
