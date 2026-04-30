Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CscPath {
    return 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
}

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function Get-FrameworkReferencePaths {
    $frameworkDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
    return @(
        (Join-Path $frameworkDir 'System.dll'),
        (Join-Path $frameworkDir 'System.Core.dll'),
        (Join-Path $frameworkDir 'System.Windows.Forms.dll'),
        (Join-Path $frameworkDir 'System.Drawing.dll'),
        (Join-Path $frameworkDir 'System.Runtime.WindowsRuntime.dll')
    )
}

function Compile-TestAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$RelativeSourceFiles
    )

    $projectRoot = Get-ProjectRoot
    $resolvedSources = foreach ($relativePath in $RelativeSourceFiles) {
        $absolutePath = Join-Path $projectRoot $relativePath
        if (-not (Test-Path -LiteralPath $absolutePath)) {
            throw "Missing source file: $relativePath"
        }

        $absolutePath
    }

    $outputDir = Join-Path $projectRoot 'obj\tests'
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    $outputPath = Join-Path $outputDir ("MediaPlayerGlobalHotkeys.Tests.{0}.dll" -f ([Guid]::NewGuid().ToString('N')))
    $compiler = Get-CscPath
    $references = Get-FrameworkReferencePaths | ForEach-Object { "/reference:$_" }
    $arguments = @(
        '/nologo'
        '/target:library'
        "/out:$outputPath"
    ) + $references + $resolvedSources

    & $compiler @arguments | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "Test assembly compilation failed."
    }

    return $outputPath
}

function Import-TestAssembly {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$RelativeSourceFiles
    )

    $assemblyPath = Compile-TestAssembly -RelativeSourceFiles $RelativeSourceFiles
    $resolvedAssemblyPath = (Resolve-Path -LiteralPath $assemblyPath).Path
    return [System.Reflection.Assembly]::LoadFile($resolvedAssemblyPath)
}

function Invoke-StaticMethod {
    param(
        [Parameter(Mandatory = $true)]
        [System.Reflection.Assembly]$Assembly,

        [Parameter(Mandatory = $true)]
        [string]$TypeName,

        [Parameter(Mandatory = $true)]
        [string]$MethodName,

        [Parameter()]
        [object[]]$Arguments = @()
    )

    $targetType = $Assembly.GetType($TypeName, $true)
    $targetMethod = $targetType.GetMethod($MethodName)

    if ($null -eq $targetMethod) {
        throw "Method not found: $TypeName::$MethodName"
    }

    return $targetMethod.Invoke($null, $Arguments)
}
