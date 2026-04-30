# MediaPlayerGlobalHotkeys

[![CI](https://github.com/gary1110086/media-player-global-hotkeys/actions/workflows/ci.yml/badge.svg)](https://github.com/gary1110086/media-player-global-hotkeys/actions/workflows/ci.yml)

`MediaPlayerGlobalHotkeys` is a small Windows helper that lets you control the built-in Media Player with global hotkeys even when the player is not focused.

It talks to the Windows media session API directly and targets the built-in Media Player session (`Microsoft.ZuneMusic` / `Microsoft.ZuneVideo`) instead of sending generic keystrokes to whatever window happens to be active.

## What it does

- `Ctrl+Alt+Space`: toggle play/pause
- `Ctrl+Alt+Left`: seek backward 5 seconds
- `Ctrl+Alt+Right`: seek forward 5 seconds
- repeated seek keeps using the latest requested target for a short window, which avoids snapping back to a stale timeline position
- holding `Ctrl+Alt+Left` or `Ctrl+Alt+Right` triggers repeated seek after a short delay

## What the main code is

The main deliverable is the compiled Windows helper:

```text
bin\MediaPlayerGlobalHotkeys.exe
```

The PowerShell file `scripts\build.ps1` is only the local build script. It is not the app itself.

The helper has no visible UI. It runs in the background and listens for global hotkeys.

If you want to read the code starting from the real entry point:

- [Program.cs](src/MediaPlayerGlobalHotkeys/Program.cs): application entry point and single-instance startup
- [HotkeyAppContext.cs](src/MediaPlayerGlobalHotkeys/HotkeyAppContext.cs): background app context and default hotkey bindings
- [LowLevelKeyboardHotkeyMonitor.cs](src/MediaPlayerGlobalHotkeys/LowLevelKeyboardHotkeyMonitor.cs): low-level keyboard hook and held-seek repeat logic
- [MediaPlayerController.cs](src/MediaPlayerGlobalHotkeys/MediaPlayerController.cs): Windows media session control and seek state handling
- [build.ps1](scripts/build.ps1): local build script

## How it works

```mermaid
flowchart LR
    A[Ctrl+Alt+Space / Left / Right] --> B[LowLevelKeyboardHotkeyMonitor]
    B --> C[MediaPlayerController]
    C --> D[Windows media session API]
    D --> E[Built-in Media Player]
```

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

## Test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

## Run

```powershell
.\bin\MediaPlayerGlobalHotkeys.exe
```

Logs are written to `bin\logs\MediaPlayerGlobalHotkeys.log`.

## Environment

- Windows
- built-in Windows Media Player app with a seekable media session
- .NET Framework compiler from `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

## Notes

- a short development note is kept in [development-notes.md](docs/development-notes.md)
