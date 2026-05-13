# RamGuardian

RamGuardian is a small Windows RAM utility with two modes:

- a stronger manual clean button
- a conservative auto-clean mode that can stay in the tray without becoming its own problem

The point is not to aggressively trim memory all the time. The point is to stay lightweight, react to real pressure, and avoid clumsy cleanup behavior during active fullscreen workloads.

## Current State

The repository currently includes a working native prototype with:

- a borderless dark WPF window
- live RAM usage text
- manual clean execution
- pressure-driven auto-clean
- tray minimize / restore behavior
- tray menu actions for `Open`, `Clean Ram`, `Auto-Clean`, and `Exit`
- persisted auto-clean state between launches
- lightweight local activity logging
- unit tests for the cleanup policy layer

## Stack

RamGuardian is built with C#, WPF, and Win32 interop.

Why this stack:

- Native Windows UI without a browser runtime.
- Easy access to Windows memory APIs through P/Invoke.
- Straightforward tray support, custom borderless chrome, and single-file publishing.
- Good GitHub ergonomics for tests, CI, and future iteration.

## Current Behavior

- `Clean Ram`
  Runs the stronger manual cleanup path. It purges standby memory and trims eligible background working sets. On severe pressure outside fullscreen workloads, it can escalate further.

- `Auto-Clean`
  Waits for real memory pressure, respects cooldowns, and uses a lighter profile during active fullscreen use when possible.

- `Hide to tray`
  The custom top-right close control hides the window to the tray instead of fully exiting.

- `Stop and exit program`
  Fully shuts the app down.

- Tray behavior
  The tray icon supports reopen, manual clean, auto-clean toggle, and full exit without reopening the main window first.

The app currently requests administrator rights because some cleanup paths use privileged Windows memory APIs.

## Project Layout

- `src/RamGuardian.App`
  Borderless WPF shell, tray integration, persisted app state, and activity logging.

- `src/RamGuardian.Core`
  Cleanup policy, telemetry models, and native-memory engine abstractions.

- `tests/RamGuardian.Core.Tests`
  Decision-logic tests for auto-clean and manual-clean planning.

- `docs`
  Research notes and architecture decisions.

## Local Data

- `%LOCALAPPDATA%\RamGuardian\settings.json`
  Stores persisted app state such as whether auto-clean was enabled on the last run.

- `%LOCALAPPDATA%\RamGuardian\activity.log`
  Stores lightweight runtime events and cleanup summaries for tuning and troubleshooting.

## Build

```powershell
dotnet build RamGuardian.slnx --configuration Release
dotnet test RamGuardian.slnx --configuration Release
```

## Publish

```powershell
dotnet publish src\RamGuardian.App\RamGuardian.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Release Package

```powershell
.\scripts\Publish-Release.ps1 -Version 0.1.3
```

This produces a local release zip at `artifacts\RamGuardian-0.1.3-win-x64.zip`.

Pushing a tag like `v0.1.3` triggers the GitHub Actions release job and publishes the same zip to GitHub Releases.
