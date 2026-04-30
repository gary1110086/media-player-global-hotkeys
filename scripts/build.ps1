Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceDir = Join-Path $projectRoot 'src\MediaPlayerGlobalHotkeys'
$outputDir = Join-Path $projectRoot 'bin'
$outputPath = Join-Path $outputDir 'MediaPlayerGlobalHotkeys.exe'
$stagingDir = Join-Path $env:TEMP 'MediaPlayerGlobalHotkeysBuild'
$stagingOutputPath = Join-Path $stagingDir 'MediaPlayerGlobalHotkeys.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'

if (-not (Test-Path -LiteralPath $sourceDir)) {
    throw "Source directory not found: $sourceDir"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $stagingOutputPath -ErrorAction SilentlyContinue

$sourceFiles = Get-ChildItem -LiteralPath $sourceDir -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }

if (-not $sourceFiles) {
    throw "No C# source files found in $sourceDir"
}

$references = @(
    (Join-Path $frameworkDir 'System.dll'),
    (Join-Path $frameworkDir 'System.Core.dll'),
    (Join-Path $frameworkDir 'System.Windows.Forms.dll'),
    (Join-Path $frameworkDir 'System.Drawing.dll'),
    (Join-Path $frameworkDir 'System.Runtime.WindowsRuntime.dll')
) | ForEach-Object { "/reference:$_" }

$arguments = @(
    '/nologo'
    '/target:winexe'
    "/out:$stagingOutputPath"
) + $references + $sourceFiles

Push-Location $stagingDir
& $compiler @arguments
$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -ne 0) {
    throw "Build failed."
}

Copy-Item -LiteralPath $stagingOutputPath -Destination $outputPath -Force

Write-Host "Built $outputPath"
