Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\TestHelpers.ps1"

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter()]
        $Actual,

        [Parameter()]
        $Expected
    )

    if ($Actual -ne $Expected) {
        throw "Assertion failed for '$Name'. Expected '$Expected' but got '$Actual'."
    }

    Write-Host "PASS: $Name"
}

$testAssembly = Import-TestAssembly -RelativeSourceFiles @(
    'src\MediaPlayerGlobalHotkeys\MediaPlayerTarget.cs',
    'src\MediaPlayerGlobalHotkeys\MediaTimelineMath.cs'
)

Assert-Equal -Name 'MediaPlayerTarget matches built-in app id' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaPlayerTarget' -MethodName 'IsTarget' -Arguments @('Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic')) `
    -Expected $true

Assert-Equal -Name 'MediaPlayerTarget matches built-in video app id' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaPlayerTarget' -MethodName 'IsTarget' -Arguments @('Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo')) `
    -Expected $true

Assert-Equal -Name 'MediaPlayerTarget rejects unrelated app ids' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaPlayerTarget' -MethodName 'IsTarget' -Arguments @('msedge.exe')) `
    -Expected $false

Assert-Equal -Name 'MediaTimelineMath clamps backward seek at minimum' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ClampSeekTargetTicks' -Arguments @(30000000L, 0L, 600000000L, -50000000L)) `
    -Expected 0L

Assert-Equal -Name 'MediaTimelineMath clamps forward seek at maximum' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ClampSeekTargetTicks' -Arguments @(590000000L, 0L, 600000000L, 50000000L)) `
    -Expected 600000000L

Assert-Equal -Name 'MediaTimelineMath prefers the last requested forward target while timeline is stale' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ResolveSeekBaseTicks' -Arguments @(1000000000L, 0L, 6000000000L, 50000000L, 1050000000L, $true, $true, 2000000L, 7500000L)) `
    -Expected 1050000000L

Assert-Equal -Name 'MediaTimelineMath prefers the last requested backward target while timeline is stale' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ResolveSeekBaseTicks' -Arguments @(1000000000L, 0L, 6000000000L, -50000000L, 950000000L, $true, $true, 2000000L, 7500000L)) `
    -Expected 950000000L

Assert-Equal -Name 'MediaTimelineMath falls back to the reported position after the stale window expires' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ResolveSeekBaseTicks' -Arguments @(1000000000L, 0L, 6000000000L, 50000000L, 1050000000L, $true, $true, 9000000L, 7500000L)) `
    -Expected 1000000000L

Assert-Equal -Name 'MediaTimelineMath keeps the last requested forward chain as the base when reversing quickly' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ResolveSeekBaseTicks' -Arguments @(1000000000L, 0L, 6000000000L, -50000000L, 1300000000L, $true, $true, 2000000L, 7500000L)) `
    -Expected 1300000000L

Assert-Equal -Name 'MediaTimelineMath keeps the last requested backward chain as the base when nudging forward quickly' `
    -Actual (Invoke-StaticMethod -Assembly $testAssembly -TypeName 'MediaTimelineMath' -MethodName 'ResolveSeekBaseTicks' -Arguments @(1000000000L, 0L, 6000000000L, 50000000L, 950000000L, $true, $true, 2000000L, 7500000L)) `
    -Expected 950000000L

$hotkeyAssembly = Import-TestAssembly -RelativeSourceFiles @(
    'src\MediaPlayerGlobalHotkeys\DesktopSessionBridge.cs',
    'src\MediaPlayerGlobalHotkeys\Program.cs',
    'src\MediaPlayerGlobalHotkeys\HotkeyAppContext.cs',
    'src\MediaPlayerGlobalHotkeys\LowLevelKeyboardHotkeyMonitor.cs',
    'src\MediaPlayerGlobalHotkeys\MediaPlayerController.cs',
    'src\MediaPlayerGlobalHotkeys\MediaPlayerTarget.cs',
    'src\MediaPlayerGlobalHotkeys\MediaTimelineMath.cs',
    'src\MediaPlayerGlobalHotkeys\SimpleLog.cs',
    'src\MediaPlayerGlobalHotkeys\SingleInstanceGate.cs'
)

$bindings = Invoke-StaticMethod -Assembly $hotkeyAssembly -TypeName 'HotkeyAppContext' -MethodName 'GetDefaultBindings'
Assert-Equal -Name 'HotkeyAppContext exposes three default bindings' -Actual $bindings.Count -Expected 3
Assert-Equal -Name 'Toggle binding uses Space' -Actual (($bindings | Where-Object { $_.Name -eq 'TogglePlayPause' }).VirtualKey) -Expected 32
Assert-Equal -Name 'Backward binding uses Left' -Actual (($bindings | Where-Object { $_.Name -eq 'SeekBackward' }).VirtualKey) -Expected 37
Assert-Equal -Name 'Forward binding uses Right' -Actual (($bindings | Where-Object { $_.Name -eq 'SeekForward' }).VirtualKey) -Expected 39
Assert-Equal -Name 'DesktopSessionBridge does not relaunch on the interactive Default desktop' -Actual (Invoke-StaticMethod -Assembly $hotkeyAssembly -TypeName 'DesktopSessionBridge' -MethodName 'ShouldRelaunchOnDefaultDesktop' -Arguments @('Default')) -Expected $false
Assert-Equal -Name 'DesktopSessionBridge relaunches from sandbox desktops' -Actual (Invoke-StaticMethod -Assembly $hotkeyAssembly -TypeName 'DesktopSessionBridge' -MethodName 'ShouldRelaunchOnDefaultDesktop' -Arguments @('CodexSandboxDesktop-abc')) -Expected $true

$recognizer = $hotkeyAssembly.CreateInstance('LowLevelHotkeyRecognizer')
Assert-Equal -Name 'LowLevelHotkeyRecognizer maps Ctrl+Alt+Space to toggle' -Actual ($recognizer.ProcessKeyDown(32, $true, $true)) -Expected 'TogglePlayPause'
Assert-Equal -Name 'LowLevelHotkeyRecognizer suppresses repeated keydown while held' -Actual ($recognizer.ProcessKeyDown(32, $true, $true)) -Expected $null
$recognizer.ProcessKeyUp(32) | Out-Null
Assert-Equal -Name 'LowLevelHotkeyRecognizer allows trigger again after keyup' -Actual ($recognizer.ProcessKeyDown(32, $true, $true)) -Expected 'TogglePlayPause'
$recognizer.ProcessKeyUp(32) | Out-Null
Assert-Equal -Name 'LowLevelHotkeyRecognizer ignores keys without modifiers' -Actual ($recognizer.ProcessKeyDown(32, $false, $false)) -Expected $null
$recognizer.ProcessKeyUp(32) | Out-Null
Assert-Equal -Name 'LowLevelHotkeyRecognizer maps Ctrl+Alt+Left to backward seek' -Actual ($recognizer.ProcessKeyDown(37, $true, $true)) -Expected 'SeekBackward'
$recognizer.ProcessKeyUp(37) | Out-Null
Assert-Equal -Name 'LowLevelHotkeyRecognizer maps Ctrl+Alt+Right to forward seek' -Actual ($recognizer.ProcessKeyDown(39, $true, $true)) -Expected 'SeekForward'

$repeatControllerType = $hotkeyAssembly.GetType('HeldSeekRepeatController', $true)
$seekBindingType = $hotkeyAssembly.GetType('HotkeyBindingInfo', $true)
$modifiersType = $hotkeyAssembly.GetType('HotkeyModifiers', $true)
$seekBindings = [Array]::CreateInstance($seekBindingType, 2)
$seekBindings.SetValue([Activator]::CreateInstance($seekBindingType, @(2, 'SeekBackward', [Enum]::ToObject($modifiersType, 3), 0x25, -5)), 0)
$seekBindings.SetValue([Activator]::CreateInstance($seekBindingType, @(3, 'SeekForward', [Enum]::ToObject($modifiersType, 3), 0x27, 5)), 1)
$repeatController = [Activator]::CreateInstance(
    $repeatControllerType,
    $seekBindings,
    2000000L,
    1000000L)

$repeatController.StartHold('SeekForward', 39, 0L) | Out-Null
Assert-Equal -Name 'HeldSeekRepeatController waits for the initial repeat delay' -Actual ($repeatController.ConsumeDueAction($true, $true, 1500000L)) -Expected $null
Assert-Equal -Name 'HeldSeekRepeatController emits a seek repeat after the initial delay' -Actual ($repeatController.ConsumeDueAction($true, $true, 2000000L)) -Expected 'SeekForward'
Assert-Equal -Name 'HeldSeekRepeatController keeps emitting repeats at the configured cadence' -Actual ($repeatController.ConsumeDueAction($true, $true, 3000000L)) -Expected 'SeekForward'
$repeatController.StopHold(39)
Assert-Equal -Name 'HeldSeekRepeatController stops after keyup' -Actual ($repeatController.ConsumeDueAction($true, $true, 4000000L)) -Expected $null
$repeatController.StartHold('SeekBackward', 37, 0L) | Out-Null
Assert-Equal -Name 'HeldSeekRepeatController stops when modifiers are released' -Actual ($repeatController.ConsumeDueAction($false, $true, 2000000L)) -Expected $null
Assert-Equal -Name 'HeldSeekRepeatController remains stopped after modifier release' -Actual ($repeatController.ConsumeDueAction($true, $true, 3000000L)) -Expected $null

$integrationAssembly = Import-TestAssembly -RelativeSourceFiles @(
    'src\MediaPlayerGlobalHotkeys\DesktopSessionBridge.cs',
    'src\MediaPlayerGlobalHotkeys\Program.cs',
    'src\MediaPlayerGlobalHotkeys\HotkeyAppContext.cs',
    'src\MediaPlayerGlobalHotkeys\LowLevelKeyboardHotkeyMonitor.cs',
    'src\MediaPlayerGlobalHotkeys\MediaPlayerController.cs',
    'src\MediaPlayerGlobalHotkeys\MediaPlayerTarget.cs',
    'src\MediaPlayerGlobalHotkeys\MediaTimelineMath.cs',
    'src\MediaPlayerGlobalHotkeys\SimpleLog.cs',
    'src\MediaPlayerGlobalHotkeys\SingleInstanceGate.cs'
)

Assert-Equal -Name 'MediaPlayerController targets the built-in media session app id prefix' `
    -Actual (Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'MediaPlayerController' -MethodName 'GetTargetAppIdPrefix') `
    -Expected 'Microsoft.ZuneMusic'

Assert-Equal -Name 'MediaPlayerController resolves the Windows media session manager type' `
    -Actual (Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'MediaPlayerController' -MethodName 'GetSessionManagerTypeName') `
    -Expected 'Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager'

Assert-Equal -Name 'SimpleLog uses a logs subdirectory under the app base path' `
    -Actual (Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'SimpleLog' -MethodName 'GetDefaultLogPath' -Arguments @('C:\Temp\AppBase')) `
    -Expected 'C:\Temp\AppBase\logs\MediaPlayerGlobalHotkeys.log'

$instanceName = 'MediaPlayerGlobalHotkeys.Tests.' + [Guid]::NewGuid().ToString('N')
$firstGate = Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'SingleInstanceGate' -MethodName 'TryAcquire' -Arguments @($instanceName)

try {
    Assert-Equal -Name 'SingleInstanceGate acquires the first instance owner' -Actual ($null -ne $firstGate) -Expected $true

    $secondGate = Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'SingleInstanceGate' -MethodName 'TryAcquire' -Arguments @($instanceName)

    try {
        Assert-Equal -Name 'SingleInstanceGate rejects a second concurrent owner' -Actual ($null -eq $secondGate) -Expected $true
    }
    finally {
        if ($null -ne $secondGate) {
            $secondGate.Dispose()
        }
    }
}
finally {
    if ($null -ne $firstGate) {
        $firstGate.Dispose()
    }
}

$thirdGate = Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'SingleInstanceGate' -MethodName 'TryAcquire' -Arguments @($instanceName)

try {
    Assert-Equal -Name 'SingleInstanceGate allows acquisition again after release' -Actual ($null -ne $thirdGate) -Expected $true
}
finally {
    if ($null -ne $thirdGate) {
        $thirdGate.Dispose()
    }
}

Assert-Equal -Name 'Hotkey startup summary reports full registration success' `
    -Actual (Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'HotkeyStartupStatus' -MethodName 'BuildRegistrationSummary' -Arguments @(3, @())) `
    -Expected 'Registered 3 of 3 hotkeys.'

Assert-Equal -Name 'Hotkey startup summary lists failed registrations' `
    -Actual (Invoke-StaticMethod -Assembly $integrationAssembly -TypeName 'HotkeyStartupStatus' -MethodName 'BuildRegistrationSummary' -Arguments @(3, @('TogglePlayPause', 'SeekBackward'))) `
    -Expected 'Registered 1 of 3 hotkeys. Failed: TogglePlayPause, SeekBackward.'

$projectRoot = Get-ProjectRoot
$buildScriptPath = Join-Path $projectRoot 'scripts\build.ps1'
$outputPath = Join-Path $projectRoot 'bin\MediaPlayerGlobalHotkeys.exe'

& $buildScriptPath
Assert-Equal -Name 'build script produces helper executable' -Actual (Test-Path -LiteralPath $outputPath) -Expected $true
