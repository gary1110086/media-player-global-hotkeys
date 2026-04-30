# MediaPlayerGlobalHotkeys

[![CI](https://github.com/gary1110086/media-player-global-hotkeys/actions/workflows/ci.yml/badge.svg)](https://github.com/gary1110086/media-player-global-hotkeys/actions/workflows/ci.yml)

Chinese version: [README.zh-CN.md](README.zh-CN.md)

`MediaPlayerGlobalHotkeys` is a small Windows helper for the built-in Media Player. It gives you a few reliable global hotkeys, so you can play, pause, and seek without dragging the player back to the foreground every time.

Under the hood it talks to the Windows media session API directly instead of sending generic keystrokes to whatever window happens to be active.

## Quick Start

If you are just here to use the app, this is the only part you need:

1. Open the [latest release](https://github.com/gary1110086/media-player-global-hotkeys/releases/latest).
2. Download the `MediaPlayerGlobalHotkeys-...zip` asset.
3. Unzip it anywhere you like.
4. Run `MediaPlayerGlobalHotkeys.exe`.

Notes:

- The helper has no visible UI. It starts in the background and keeps listening for hotkeys.
- If the release page also shows `Source code (zip)` and `Source code (tar.gz)`, those are automatic source archives from GitHub. They are for people who want the source, not for people who just want the app.

## Hotkeys

- `Ctrl+Alt+Space`: play or pause
- `Ctrl+Alt+Left`: seek backward 5 seconds
- `Ctrl+Alt+Right`: seek forward 5 seconds
- Hold `Ctrl+Alt+Left` or `Ctrl+Alt+Right` to keep seeking after a short delay

## Requirements

- Windows
- The built-in Media Player app
- A loaded media session that can actually play and seek

## Troubleshooting

- If nothing happens, make sure Media Player is open and already has playable media loaded.
- Logs are written to a `logs` folder next to the executable.

## For Developers

If you want to inspect the code or build the helper yourself, start here. Otherwise, you can ignore the rest of this README.

- [Program.cs](src/MediaPlayerGlobalHotkeys/Program.cs): application entry point and single-instance startup
- [HotkeyAppContext.cs](src/MediaPlayerGlobalHotkeys/HotkeyAppContext.cs): background app context and default hotkey bindings
- [LowLevelKeyboardHotkeyMonitor.cs](src/MediaPlayerGlobalHotkeys/LowLevelKeyboardHotkeyMonitor.cs): low-level keyboard hook and held-seek repeat logic
- [MediaPlayerController.cs](src/MediaPlayerGlobalHotkeys/MediaPlayerController.cs): Windows media session control and seek state handling
- [build.ps1](scripts/build.ps1): local build script

The local build output is `bin\MediaPlayerGlobalHotkeys.exe`.

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

## Test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

## How It Works

```mermaid
flowchart LR
    A[Ctrl+Alt+Space / Left / Right] --> B[LowLevelKeyboardHotkeyMonitor]
    B --> C[MediaPlayerController]
    C --> D[Windows media session API]
    D --> E[Built-in Media Player]
```

## Development Notes

- A short project note is kept in [development-notes.md](docs/development-notes.md).
- The current local build expects the .NET Framework compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
