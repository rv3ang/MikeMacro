# MikeMacro

Cross-platform macro automation engine inspired by TGMacro.

## Direction

MikeMacro is being built as a portable .NET 8 application with a small, testable
core. The core owns macro data, validation, persistence, recording semantics,
and playback orchestration. Operating-system integrations live behind
`IInputBackend`, so Windows, macOS, and Linux can provide native input adapters
without changing macro files or the player.

The reference project is a useful feature reference, but its WinForms and
Windows input-library coupling do not scale to every device. This project keeps
the macro format device-neutral and treats keyboards, mice, controllers, and
future device integrations as adapters.

## Project layout

- `src/MikeMacro.Core`: platform-neutral models, JSON persistence, and playback
- `tests/MikeMacro.Core.Tests`: deterministic unit tests using a fake backend
- `docs/EXECUTION_PLAN.md`: full roadmap, flows, architecture, and folder structure
- `src/MikeMacro.Desktop`: first Avalonia desktop shell with safe preview mode
- `src/MikeMacro.Platform.Windows`: Windows `SendInput` backend with held-input cleanup
- `src/MikeMacro.Platform.Windows`: Windows `SendInput` backend and global hotkey registration
- `src/MikeMacro.Platform.Windows`: Windows low-level keyboard/mouse capture for recording

## First milestone

The initial core supports keyboard actions, mouse buttons, mouse movement,
text input, delays, repeat counts, cancellation, and JSON round-tripping.

## Build and test

```bash
dotnet test
```

The desktop UI and native backends will be added after the core contract is
stable. This keeps hardware-specific behavior isolated and testable.

See [docs/EXECUTION_PLAN.md](docs/EXECUTION_PLAN.md) for the complete execution
plan and the next sprint scope.

To start the desktop shell locally:

```bash
dotnet run --project src/MikeMacro.Desktop/MikeMacro.Desktop.csproj
```

The Windows backend compiles in this environment but must be smoke-tested on a
Windows session before real input is enabled in the desktop application.

Windows recording uses low-level hooks and ignores injected events, preventing
MikeMacro playback from recording itself. The first run must be smoke-tested in
a Windows desktop session with the required input permissions.
