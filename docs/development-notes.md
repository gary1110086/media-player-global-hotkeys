# Development Notes

## Current behavior

- `Ctrl+Alt+Space` toggles play/pause through the Windows media session API.
- `Ctrl+Alt+Left` seeks backward by 5 seconds.
- `Ctrl+Alt+Right` seeks forward by 5 seconds.
- seek works even when Media Player is unfocused.

## Important implementation details

- repeated seek uses a short-lived requested-target chain so fast key presses do not jump back to a stale reported timeline position
- holding `Ctrl+Alt+Left` or `Ctrl+Alt+Right` starts repeated seek after a short delay
- the helper relaunches onto the interactive desktop when started from a non-default desktop

## Tunable constants

- `MediaPlayerController.RecentSeekStateWindow = 1000ms`
- `LowLevelKeyboardHotkeyMonitor.HeldSeekInitialDelay = 250ms`
- `LowLevelKeyboardHotkeyMonitor.HeldSeekRepeatInterval = 160ms`
