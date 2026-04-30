# Media Player Global Hotkeys Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a small Windows background helper that registers global hotkeys and controls the built-in Windows `媒体播放器` app while it is unfocused.

**Architecture:** Compile a small .NET Framework / WinForms background app with a hidden message window. Use Win32 `RegisterHotKey` for global key capture and call the Windows media session API through reflection-based WinRT access so the helper only controls `Microsoft.ZuneMusic`. Keep the seek math and target-app filtering isolated in pure static helpers and verify them first with Pester.

**Tech Stack:** C#, .NET Framework `csc.exe`, WinForms, Win32 P/Invoke, PowerShell build/test scripts, Pester 3.4

---

## Implementation Adjustment

The approved design targeted the Windows media session API directly. During execution, the local machine was found to have the runtime but not the SDK / usable WinRT compile-time metadata needed for a straightforward C# implementation of `Windows.Media.Control`. The first implemented revision therefore keeps the C# global hotkey shell but targets the packaged `Microsoft.Media.Player` process window directly with Win32 keyboard messages.

This preserves the user-visible goal:

- global hotkeys work while the player is unfocused
- targeting remains limited to the built-in Media Player app

The trade-off is that seek behavior now depends on the player's own key handling for `Left` and `Right`, so final behavior still needs live manual verification with an open video.

## Resume State

Current implemented state as of 2026-04-29:

- `bin\MediaPlayerGlobalHotkeys.exe` builds and starts successfully in the background.
- The helper currently has no tray icon or visible startup indicator.
- Launching multiple instances causes all three hotkey registrations to fail because they compete for the same global keys.
- A log file is written to `bin\logs\MediaPlayerGlobalHotkeys.log`.

Next iteration should start here:

1. Add single-instance protection so only one helper can run at a time.
2. Add clearer visibility for startup state and hotkey registration failures.
3. Stop old helper instances, launch one fresh instance, and perform live manual verification against the built-in Media Player with an actual video open.

## File Structure

- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\Program.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\HotkeyAppContext.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\MediaPlayerController.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\MediaPlayerTarget.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\MediaTimelineMath.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\SimpleLog.cs`
- Create: `D:\myListProject\修改播放器\scripts\build.ps1`
- Create: `D:\myListProject\修改播放器\tests\TestHelpers.ps1`
- Create: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`
- Output: `D:\myListProject\修改播放器\bin\MediaPlayerGlobalHotkeys.exe`

Notes:

- The workspace is not a git repository, so commit steps are recorded as explicit skips.
- The machine has `.NET runtime` but no `.NET SDK`; compilation should use `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

## Chunk 1: Testable Core and Build Scaffolding

### Task 1: Create the failing tests for target-player filtering and seek clamping

**Files:**
- Create: `D:\myListProject\修改播放器\tests\TestHelpers.ps1`
- Create: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`
- Test: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`

- [ ] **Step 1: Write the failing tests**

```powershell
Describe 'MediaPlayerTarget' {
    It 'matches the built-in media player app id' {
        [MediaPlayerTarget]::IsTarget('Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic') | Should Be $true
    }

    It 'rejects unrelated app ids' {
        [MediaPlayerTarget]::IsTarget('msedge.exe') | Should Be $false
    }
}

Describe 'MediaTimelineMath' {
    It 'seeks backward by 5 seconds without going below the minimum' {
        [MediaTimelineMath]::ClampSeekTargetTicks(30000000L, 0L, 600000000L, -50000000L) | Should Be 0L
    }

    It 'seeks forward by 5 seconds without exceeding the maximum' {
        [MediaTimelineMath]::ClampSeekTargetTicks(590000000L, 0L, 600000000L, 50000000L) | Should Be 600000000L
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: FAIL because the helper source files and types do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `MediaPlayerTarget.cs` with a single `IsTarget(string appId)` method and `MediaTimelineMath.cs` with a single `ClampSeekTargetTicks(long current, long min, long max, long delta)` method.

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: PASS for the target filter and seek clamp cases.

- [ ] **Step 5: Commit**

Skip: workspace is not a git repository.

### Task 2: Add a failing build smoke test and compile script

**Files:**
- Create: `D:\myListProject\修改播放器\scripts\build.ps1`
- Modify: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`
- Test: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`

- [ ] **Step 1: Extend tests with a failing build smoke check**

```powershell
Describe 'build script' {
    It 'produces the helper executable' {
        & "$PSScriptRoot\..\scripts\build.ps1"
        Test-Path "$PSScriptRoot\..\bin\MediaPlayerGlobalHotkeys.exe" | Should Be $true
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: FAIL because the build script and application entry files do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `build.ps1` that:

- ensures `bin\` exists
- compiles all `src\MediaPlayerGlobalHotkeys\*.cs` files
- uses:

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

- references:

```text
System.Windows.Forms.dll
System.Drawing.dll
System.Runtime.WindowsRuntime.dll
```

- writes:

```text
bin\MediaPlayerGlobalHotkeys.exe
```

Create a minimal `Program.cs` that starts and exits cleanly so the build can succeed before the hotkey logic exists.

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: PASS for both the unit tests and the build smoke test.

- [ ] **Step 5: Commit**

Skip: workspace is not a git repository.

## Chunk 2: Hidden Hotkey App and Media Session Control

### Task 3: Add the hidden message loop and global hotkey handling

**Files:**
- Modify: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\Program.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\HotkeyAppContext.cs`
- Modify: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`
- Test: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`

- [ ] **Step 1: Add a failing test for the expected hotkey definitions**

```powershell
Describe 'hotkey definitions' {
    It 'uses Ctrl+Alt+Space, Ctrl+Alt+Left, and Ctrl+Alt+Right' {
        $bindings = [HotkeyAppContext]::GetDefaultBindings()
        $bindings.Count | Should Be 3
        ($bindings | Where-Object { $_.Name -eq 'TogglePlayPause' }).VirtualKey | Should Be 32
        ($bindings | Where-Object { $_.Name -eq 'SeekBackward' }).VirtualKey | Should Be 37
        ($bindings | Where-Object { $_.Name -eq 'SeekForward' }).VirtualKey | Should Be 39
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: FAIL because `HotkeyAppContext` and the binding metadata are not implemented.

- [ ] **Step 3: Write minimal implementation**

Implement `HotkeyAppContext.cs` with:

- the three default hotkey definitions
- `RegisterHotKey` / `UnregisterHotKey` P/Invoke
- a hidden message window or application context that processes `WM_HOTKEY`
- a dispatch path that calls a controller interface for:
  - toggle play/pause
  - seek backward 5 seconds
  - seek forward 5 seconds

Update `Program.cs` to run the hidden application context rather than exiting immediately.

- [ ] **Step 4: Run test to verify it passes**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: PASS for hotkey definition metadata, previous unit tests, and build smoke.

- [ ] **Step 5: Commit**

Skip: workspace is not a git repository.

### Task 4: Implement Media Player session control and manual verification flow

**Files:**
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\MediaPlayerController.cs`
- Create: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\SimpleLog.cs`
- Modify: `D:\myListProject\修改播放器\src\MediaPlayerGlobalHotkeys\HotkeyAppContext.cs`
- Modify: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`
- Test: `D:\myListProject\修改播放器\tests\MediaPlayerGlobalHotkeys.Tests.ps1`

- [ ] **Step 1: Add a failing test for app-id matching behavior used by the controller**

```powershell
Describe 'controller app targeting' {
    It 'treats Microsoft.ZuneMusic sessions as eligible targets' {
        [MediaPlayerTarget]::IsTarget('Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic') | Should Be $true
    }
}
```

Expected: this should already pass, so if the controller needs a new helper method, write a new failing test for that helper first instead of changing production code directly.

- [ ] **Step 2: Run test to verify RED where new behavior is added**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

Expected: FAIL only for the new helper or controller-facing behavior added in this task.

- [ ] **Step 3: Write minimal implementation**

Implement `MediaPlayerController.cs` so it:

- resolves `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` via:

```csharp
Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows, ContentType=WindowsRuntime")
```

- requests the session manager asynchronously using reflection
- gets the current session
- filters by `MediaPlayerTarget.IsTarget(sourceAppUserModelId)`
- toggles play/pause through the session method
- reads timeline properties and computes a clamped seek target using `MediaTimelineMath`
- changes playback position through the session seek method
- returns without throwing for normal no-op states

Implement `SimpleLog.cs` with a minimal file logger for startup failures such as hotkey registration conflicts.

Wire `HotkeyAppContext` to call `MediaPlayerController` for each hotkey action.

- [ ] **Step 4: Run automated checks**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

Expected: PASS and a fresh `bin\MediaPlayerGlobalHotkeys.exe`.

- [ ] **Step 5: Run manual verification**

1. Start `bin\MediaPlayerGlobalHotkeys.exe`.
2. Open the built-in Windows `媒体播放器`.
3. Play a seekable video.
4. Switch focus to another application.
5. Press:
   - `Ctrl+Alt+Space`
   - `Ctrl+Alt+Left`
   - `Ctrl+Alt+Right`
6. Confirm:
   - play/pause toggles
   - backward seek moves about 5 seconds
   - forward seek moves about 5 seconds
7. Confirm no crash when the player is closed.

- [ ] **Step 6: Commit**

Skip: workspace is not a git repository.
