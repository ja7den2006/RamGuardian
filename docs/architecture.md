# Architecture

## Goals

- Keep the UI extremely small and focused.
- Make cleanup decisions testable before touching native calls.
- Separate safe auto-clean policy from stronger manual cleanup behavior.
- Keep idle overhead low enough that leaving the app open is reasonable.

## Project Structure

### `RamGuardian.App`

Responsibilities:

- Borderless dark window
- Current RAM usage text
- Three-button UI
- Color/state transitions
- Minimize-to-tray behavior
- Tray right-click menu with full exit

### `RamGuardian.Core`

Responsibilities:

- Memory snapshot models
- Auto-clean thresholds and cooldowns
- Cleanup plan construction
- Native memory-cleaning engine abstractions
- Native Windows interop implementation

### `RamGuardian.Core.Tests`

Responsibilities:

- Auto-clean decision tests
- Manual-clean plan tests
- Regression coverage for cooldown and fullscreen rules

## Cleanup Profiles

### Auto Low-Priority Standby

Used when:

- memory pressure exists
- a game or other full-screen interactive workload is active
- pressure is real but not yet severe enough to justify stronger cleanup

Effect:

- purge low-priority standby memory only

### Auto Standby

Used when:

- memory pressure is sustained
- available memory is critically low
- commit pressure is high
- no active fullscreen sensitivity should block escalation

Effect:

- purge standard standby memory

### Manual Balanced

Used when:

- the user explicitly clicks `Clean Ram`

Effect:

- purge standby memory
- trim background working sets
- allow stronger system-wide trimming only when pressure is severe and the app is not trying to protect an active full-screen workload

## Auto-Clean Policy Rules

The auto-clean policy should require more than a single high number.

Inputs:

- available physical memory
- total physical memory
- commit pressure
- Windows low-memory resource signal
- fullscreen activity state
- sustained-pressure duration
- cleanup cooldown

Rules:

1. Do nothing while cooldown is active.
2. Do nothing if pressure has not lasted long enough unless Windows is already signaling low memory.
3. Prefer low-priority standby cleanup for fullscreen interactive workloads.
4. Escalate to standard standby cleanup when pressure is critical or sustained.
5. Never let auto-clean use the most aggressive trim path by default.

## Native Engine Plan

The engine will likely wrap these Windows concepts:

- `GlobalMemoryStatusEx`
- `CreateMemoryResourceNotification`
- `QueryMemoryResourceNotification`
- `GetPerformanceInfo`
- `NtSetSystemInformation`
- `EmptyWorkingSet`

The app should treat those calls as implementation details behind a narrow interface so policy can be tested without native side effects.
