# Research Notes

## ExitLag Findings

Publicly documented behavior is limited.

- ExitLag's help center describes RAM Cleaner as a one-click feature to safely free RAM.
- ExitLag says the cleanup is manual by design and does not run automatically.

Local inspection of the installed Windows build on this machine adds more signal.

- Installed version observed: `5.18.4`
- Install root observed: `D:\ExitLag`
- App type observed: native Qt application, not Electron
- Main binary contains strings for:
  - `EmptyWorkingSet`
  - `NtSetSystemInformation`
  - `GlobalMemoryStatusEx`
  - `K32GetProcessMemoryInfo`
  - `Process32FirstW`
  - `Process32NextW`
  - `PC Boost`
  - `views/PerformanceBoost/PerformanceBoost.qml`

ExitLag also stores local feature state in `C:\Users\jayde\AppData\Local\ExitLag\storage.db`, where related tables include:

- `ram_optimizer__text`
- `fps_boost__integer`
- `sys_monitor__integer`

## ExitLag Inference

The exact internal cleanup sequence is not publicly documented, so this section is an inference, not a confirmed vendor statement.

Most likely behavior:

1. Read current memory state with `GlobalMemoryStatusEx`.
2. Inspect process memory or enumerate candidates with `K32GetProcessMemoryInfo` and Toolhelp APIs.
3. Use one or both of these cleanup primitives:
   - working-set trimming through `EmptyWorkingSet` or the equivalent system-wide memory-list call
   - standby-list or related cache cleanup through `NtSetSystemInformation`

What I do not currently have direct proof of:

- the exact order of those calls
- whether ExitLag trims all processes or only selected background processes
- whether it always purges the standby list or only under certain thresholds

## Similar Tools

### Mem Reduct

Open-source and the best public reference for aggressive Windows memory cleaners.

- Uses undocumented Native API calls.
- Supports multiple regions, including:
  - working sets
  - standby lists
  - low-priority standby lists
  - modified page list
  - system file cache
  - registry cache
  - memory combine

Takeaway:

- Good reference for implementation details.
- Too aggressive to mirror blindly for auto-clean in a gaming-focused tool.

### CleanMem

Long-running Windows utility with a more conservative public explanation.

- Positions itself as asking Windows to do the work rather than fighting the memory manager.
- Runs on a schedule by default through Task Scheduler.
- Emphasizes working with process memory and cache behavior rather than deep system-wide flushing.

Takeaway:

- Good reference for low-overhead scheduling and lightweight background operation.
- Less likely to match ExitLag's perceived “strong” manual clean feeling on its own.

### ISLC

Focused almost entirely on standby-list behavior.

- Monitors and clears the memory standby list according to configured thresholds.
- Explicitly does not fix memory leaks.
- Has an exclusion concept for games or applications that behave worse when standby memory is purged.

Takeaway:

- Strong reference for auto-clean safety.
- Useful model for why auto-clean should stay mostly standby-focused.

### Wise Memory Optimizer

Commercial utility with broad claims around RAM cleanup.

- Markets one-click cleanup, RAM defragmentation, and standby-memory clearing.
- Supports threshold-based automatic cleanup when memory is low.

Takeaway:

- Product inspiration for simplicity, not a technical reference source.

## RamGuardian Decisions

### Manual Clean

Manual clean should feel effective.

Current design intent:

- Purge standby memory.
- Trim background working sets.
- Only allow system-wide working-set trimming under severe pressure.

### Auto-Clean

Auto-clean should be safer than manual clean.

Current design intent:

- Use OS pressure signals and sustained-low-memory conditions.
- Prefer low-priority standby cleanup first.
- Escalate to standard standby cleanup when pressure is sustained or critical.
- Do not automatically trim working sets system-wide during gameplay.

### Idle Overhead

The app must stay light when “doing nothing.”

Current design intent:

- One lightweight sampling loop.
- No continuous deep process scans unless a cleanup is actually being planned.
- Tray-first behavior after window close.
