# Media Player Global Hotkeys Wrap-Up

Date: 2026-04-30
Workspace: `D:\myListProject\修改播放器`

## Current Behavior

- `Ctrl+Alt+Space` toggles play/pause through the Windows media session API.
- `Ctrl+Alt+Left` seeks backward by 5 seconds.
- `Ctrl+Alt+Right` seeks forward by 5 seconds.
- Seek actions work even when Media Player is unfocused.
- Seek actions now keep a short-lived requested-target chain so fast repeated seeks do not snap back to a stale reported position.
- Holding `Ctrl+Alt+Left` or `Ctrl+Alt+Right` now repeats seek after a short delay.

## Tunable Constants

- `MediaPlayerController.RecentSeekStateWindow = 1000ms`
  File: `src\MediaPlayerGlobalHotkeys\MediaPlayerController.cs`
  Purpose: how long a recent seek chain is trusted when the player timeline has not caught up yet.

- `LowLevelKeyboardHotkeyMonitor.HeldSeekInitialDelay = 250ms`
  File: `src\MediaPlayerGlobalHotkeys\LowLevelKeyboardHotkeyMonitor.cs`
  Purpose: delay before held seek starts repeating.

- `LowLevelKeyboardHotkeyMonitor.HeldSeekRepeatInterval = 160ms`
  File: `src\MediaPlayerGlobalHotkeys\LowLevelKeyboardHotkeyMonitor.cs`
  Purpose: repeat cadence while the seek hotkey is held.

## Regression Coverage

- fast repeated forward seek uses the last requested target while timeline is stale
- fast repeated backward seek uses the last requested target while timeline is stale
- quick direction reversal keeps the seek chain instead of jumping back to the old reported position
- held seek repeat starts after the initial delay, repeats at the configured cadence, and stops on keyup or modifier release

## Manual Checkpoints

1. Press `Ctrl+Alt+Right` repeatedly with another app focused and confirm the position keeps moving forward without snapping back.
2. After several fast forward seeks, press `Ctrl+Alt+Left` once and confirm it backs up from the latest requested position rather than the old visible position.
3. Hold `Ctrl+Alt+Right` and confirm seek repeats smoothly.
4. Release `Right`, `Ctrl`, or `Alt` and confirm repeat stops immediately.

## Workspace Notes

- This workspace is still not a git repository, so no branch, commit, or PR cleanup was performed here.
- Helper logs remain under `bin\logs\MediaPlayerGlobalHotkeys.log`.
