# Media Player Global Hotkeys Design

Date: 2026-04-29
Status: Draft approved in chat, pending user review of written spec
Target workspace: `D:\myListProject\修改播放器`

## Goal

Provide global hotkeys that control the built-in Windows `媒体播放器` app even when its window is not focused.

The first version only needs three actions:

- `Ctrl+Alt+Space`: toggle play/pause
- `Ctrl+Alt+Left`: seek backward 5 seconds
- `Ctrl+Alt+Right`: seek forward 5 seconds

## Constraints

- The target player is the built-in Windows Media Player app identified by `Microsoft.ZuneMusic`.
- This is not a source modification of the system app. The solution must be an external helper process.
- The helper should work while the player window is unfocused.
- The helper should avoid controlling unrelated media apps.
- The first version should optimize for reliability over configurability.

## Recommended Approach

Implement a small C# Windows background helper that:

1. Registers three system-wide hotkeys.
2. Locates the active Windows media session that belongs to `Microsoft.ZuneMusic`.
3. Uses the Windows media session API to issue playback and seek commands directly.

This is preferred over AutoHotkey or PowerShell because it gives stable global hotkey registration, precise seek control, and explicit filtering to the built-in media player.

## Architecture

The helper will be a small background application with four responsibilities:

### 1. Hotkey Registration

- Register `Ctrl+Alt+Space`, `Ctrl+Alt+Left`, and `Ctrl+Alt+Right` as global hotkeys.
- Process hotkey events through the Windows message loop.
- Treat each hotkey press as one discrete action rather than a held-repeat acceleration feature.

### 2. Session Resolution

- Query the Windows `GlobalSystemMediaTransportControlsSessionManager`.
- Prefer the current active session when available.
- Accept the session only if its source application identifier matches `Microsoft.ZuneMusic`.
- If no matching session exists, do nothing.

### 3. Playback Control

- For play/pause, call the media session toggle API directly.
- Do not simulate keyboard input into the player window.

### 4. Seek Control

- Read current timeline properties from the matched media session.
- Compute target position as current position plus or minus 5 seconds.
- Clamp the result to the valid seekable range.
- Submit the target position through the media session seek API.

## Behavior Details

### Play/Pause

- `Ctrl+Alt+Space` toggles playback state.
- If the matched session is unavailable or cannot be controlled, ignore the hotkey.

### Backward Seek

- `Ctrl+Alt+Left` subtracts 5 seconds from the current position.
- If the current position is within 5 seconds of the beginning, clamp to the earliest valid position.

### Forward Seek

- `Ctrl+Alt+Right` adds 5 seconds to the current position.
- If the current position is within 5 seconds of the end or maximum seek position, clamp to the latest valid position.

## Failure Handling

The helper should fail quietly for normal runtime misses and only surface actionable setup issues.

### Expected Runtime No-Op Cases

- The Media Player app is not running.
- A session exists, but it is not from `Microsoft.ZuneMusic`.
- The current media item does not support seeking.
- The Windows media session manager returns no current session.

In these cases, the program should do nothing and remain running.

### Setup or Environment Problems

- A global hotkey cannot be registered because another application already owns it.
- The required media session API is unavailable or throws an initialization error.

In these cases, the program should remain alive if possible and report the problem through lightweight logging or a simple startup message.

## UX and Delivery

### First Version

- Launch as a small background program.
- No main window is required.
- No tray icon is required in the first version.
- No settings UI is required in the first version.
- Hotkeys and seek duration are hardcoded.

### Possible Future Enhancements

- System tray icon
- Configurable hotkeys
- Configurable seek duration
- Launch at login
- A minimal status page or log view

## Testing Strategy

Testing for the first version should focus on real behavior rather than large automated test investment.

### Manual Verification

1. Start the helper.
2. Open the built-in Windows `媒体播放器`.
3. Play a seekable video.
4. Switch focus to another application.
5. Press each hotkey and confirm:
   - play/pause toggles correctly
   - backward seek moves about 5 seconds
   - forward seek moves about 5 seconds
6. Verify boundary behavior near the start and end of the video.
7. Verify that the helper does nothing when the Media Player app is closed.

### Diagnostic Checks

- Confirm that hotkey registration succeeds for all three keys.
- Confirm that the resolved session source matches `Microsoft.ZuneMusic`.
- Confirm that seek requests are skipped when the session is not seekable.

## Implementation Notes

- The Windows APIs already expose both playback toggle and playback position change operations for system media sessions.
- This design intentionally avoids window-focus hacks and input simulation because direct media session control is more robust.
- The helper targets only the built-in Media Player app in the first version and does not attempt to support browsers or third-party players.

## Workspace Notes

- The current workspace is not a git repository, so this spec cannot be committed here unless the project is moved into or initialized as a repository.
- The spec path is still created in the requested workspace so implementation can continue locally.
