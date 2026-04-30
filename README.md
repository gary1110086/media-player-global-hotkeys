# MediaPlayerGlobalHotkeys

A small Windows helper that controls the built-in Media Player with global hotkeys, even when the player is not focused.

## Hotkeys

- `Ctrl+Alt+Space`: toggle play/pause
- `Ctrl+Alt+Left`: seek backward 5 seconds
- `Ctrl+Alt+Right`: seek forward 5 seconds

The helper uses the Windows media session API and targets the built-in Media Player session (`Microsoft.ZuneMusic` / `Microsoft.ZuneVideo`) instead of simulating generic keyboard input.

## Current Behavior

- Works while Media Player is unfocused
- Uses a short-lived seek target chain to avoid snapping back to a stale reported timeline position during fast repeated seek
- Supports held seek repeat for `Ctrl+Alt+Left` and `Ctrl+Alt+Right`
- Automatically relaunches onto the interactive desktop when started from a sandbox or non-default desktop

## Project Structure

- `src/MediaPlayerGlobalHotkeys/`: C# application code
- `scripts/build.ps1`: build script using the system `csc.exe`
- `tests/`: PowerShell regression checks
- `docs/superpowers/`: design notes, implementation plan, and wrap-up notes

## Build

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

This produces:

```text
bin\MediaPlayerGlobalHotkeys.exe
```

## Test

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

## Run

```powershell
.\bin\MediaPlayerGlobalHotkeys.exe
```

Logs are written to:

```text
bin\logs\MediaPlayerGlobalHotkeys.log
```

## Environment

- Windows
- .NET Framework compiler from `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- Built-in Windows Media Player app with a seekable media session

## Notes

- This workspace originally was not a git repository; repository metadata was added later for publishing.
- Generated artifacts under `bin/` and `obj/` are intentionally ignored.
