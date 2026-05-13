# RamGuardian

RamGuardian is a small Windows RAM utility focused on one job: give you a fast manual cleanup button and a conservative auto-clean mode that can stay open in the tray without becoming its own problem.

## Status

The repository is in the first implementation stage.

- Research completed for ExitLag-style RAM cleaning behavior and similar tools.
- Solution scaffolded for a native Windows desktop app.
- Core policy layer is covered by tests.
- First borderless tray UI shell is wired to the native telemetry and cleanup engine.

## Stack Choice

RamGuardian is being built with C#, WPF, and Win32 interop.

Why this stack:

- Native Windows UI without a browser runtime.
- Easy access to Windows memory APIs through P/Invoke.
- Straightforward tray support, custom borderless chrome, and single-file publishing.
- Good GitHub ergonomics for tests, CI, and future iteration.

## Design Direction

The app will intentionally separate manual cleanup from auto-clean behavior.

- Manual clean will be stronger.
- Auto-clean will be conservative and pressure-driven.
- Auto-clean will avoid aggressive working-set trimming during active full-screen use whenever possible.
- The app will minimize to tray on close, and fully exit from the tray menu.
- The app currently requests administrator rights because the cleanup path uses privileged Windows memory APIs.

## Planned Layout

- `src/RamGuardian.App`
  Borderless WPF shell, tray integration, status text, and button flows.

- `src/RamGuardian.Core`
  Cleanup policy, telemetry models, and native-memory engine abstractions.

- `tests/RamGuardian.Core.Tests`
  Decision-logic tests for auto-clean and manual-clean planning.

- `docs`
  Research notes and architecture decisions.

## Near-Term Milestones

1. Wire the core cleanup engine to Windows APIs.
2. Build the minimal dark UI shell and tray behavior.
3. Add single-file publish settings and smoke-test the tray lifecycle.
4. Tune auto-clean thresholds against real memory pressure instead of fake benchmarks.

## Build

```powershell
dotnet build RamGuardian.slnx
dotnet test RamGuardian.slnx
```
