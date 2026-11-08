<#
.SYNOPSIS
    Builds the Servora release artifacts locally - the same artifacts that .github/workflows/release.yml publishes.

.DESCRIPTION
    1. Runs the unit tests (skip with -SkipTests).
    2. Publishes src/Servora.Web self-contained for the runtime (default win-x64).
    3. Packs the published application into Servora-<version>-<runtime>.zip.
    4. Publishes src/Servora.Installer as a single-file, self-contained ServoraSetup.exe with the zip embedded as payload
       and copies it to ServoraSetup-<version>-<runtime>.exe.
    5. Writes SHA256SUMS.txt (format of `sha256sum`, verifiable with `sha256sum -c` or Get-FileHash).
    6. Smoke-tests the installer (`ServoraSetup version`), which reports whether the payload is embedded.

.PARAMETER Version
    Semantic version (for example 1.2.0 or 1.3.0-beta.1). Defaults to <Version> in Directory.Build.props.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Runtime
    .NET runtime identifier. Default: win-x64.

.PARAMETER OutputDirectory
    Output folder. Default: <repo>/artifacts.

.PARAMETER SkipTests
    Skips `dotnet test`.

.EXAMPLE
    ./build/publish.ps1
    ./build/publish.ps1 -Version 1.2.0 -SkipTests
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Configuration = 'Release',
    [string] $Runtime = 'win-x64',
    [string] $OutputDirectory,
    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts'
}

# MSBuild resolves relative paths against the project folder, so -p:PayloadZip must be absolute.
$OutputDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutputDirectory))

if (-not $Version) {
    [xml] $props = Get-Content -Raw -Path (Join-Path $repoRoot 'Directory.Build.props')
    $node = $props.SelectSingleNode('/Project/PropertyGroup/Version')
    if ($null -eq $node) { throw 'No <Version> found in Directory.Build.props; pass -Version.' }
    $Version = $node.InnerText.Trim()
}

if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    throw "Invalid version '$Version'. Expected a semantic version such as 1.2.0 or 1.3.0-beta.1."
}

function Invoke-DotNet {
    param([Parameter(Mandatory)] [string[]] $Arguments)
    Write-Host "  > dotnet $($Arguments -join ' ')" -ForegroundColor DarkGray
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

function Write-Stage([string] $Text) {
    Write-Host ''
    Write-Host "== $Text" -ForegroundColor Cyan
}

$zipName = "Servora-$Version-$Runtime.zip"
$setupName = "ServoraSetup-$Version-$Runtime.exe"
$checksumName = 'SHA256SUMS.txt'
$workDirectory = Join-Path $OutputDirectory 'work'
$appDirectory = Join-Path $workDirectory 'app'
$installerDirectory = Join-Path $workDirectory 'installer'
$zipPath = Join-Path $OutputDirectory $zipName
$setupPath = Join-Path $OutputDirectory $setupName
$checksumPath = Join-Path $OutputDirectory $checksumName

Write-Host "Servora release build $Version ($Runtime, $Configuration)" -ForegroundColor White
Write-Host "Output: $OutputDirectory"

# Only remove what this script produces; never wipe an arbitrary folder.
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
if (Test-Path $workDirectory) { Remove-Item -Recurse -Force $workDirectory }
foreach ($old in @($zipPath, $setupPath, $checksumPath)) {
    if (Test-Path $old) { Remove-Item -Force $old }
}

Push-Location $repoRoot
try {
    if (-not $SkipTests) {
        Write-Stage 'Running tests'
        Invoke-DotNet @('test', 'Servora.slnx', '-c', $Configuration, '--nologo')
    }

    Write-Stage 'Publishing Servora (self-contained)'
    Invoke-DotNet @(
        'publish', 'src/Servora.Web/Servora.Web.csproj',
        '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true',
        '-o', $appDirectory, "-p:Version=$Version", '--nologo')
    if (-not (Test-Path (Join-Path $appDirectory 'Servora.exe'))) {
        throw 'The published application does not contain Servora.exe.'
    }

    Write-Stage "Packing $zipName"
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $appDirectory, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    Write-Stage "Building installer $setupName (payload embedded)"
    Invoke-DotNet @(
        'publish', 'src/Servora.Installer/Servora.Installer.csproj',
        '-c', $Configuration, '-r', $Runtime,
        '-o', $installerDirectory, "-p:Version=$Version", "-p:PayloadZip=$zipPath", '--nologo')
    Copy-Item -Path (Join-Path $installerDirectory 'ServoraSetup.exe') -Destination $setupPath -Force

    Write-Stage 'Writing checksums'
    $lines = foreach ($file in @($zipPath, $setupPath)) {
        $hash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash.ToLowerInvariant()
        "$hash  $(Split-Path -Leaf $file)"
    }
    [System.IO.File]::WriteAllText($checksumPath, (($lines -join "`n") + "`n"), [System.Text.Encoding]::ASCII)

    Write-Stage 'Smoke test'
    $versionOutput = & $setupPath version --silent | Out-String
    if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch 'embedded payload') {
        throw "Installer smoke test failed (exit code $LASTEXITCODE):`n$versionOutput"
    }
    Write-Host $versionOutput.Trim()

    Remove-Item -Recurse -Force $workDirectory

    Write-Stage 'Artifacts'
    Get-ChildItem -Path $OutputDirectory -File |
        Where-Object { $_.Name -in @($zipName, $setupName, $checksumName) } |
        Select-Object Name, @{ Name = 'Size (MB)'; Expression = { [math]::Round($_.Length / 1MB, 1) } } |
        Format-Table -AutoSize | Out-String | Write-Host
}
finally {
    Pop-Location
}
