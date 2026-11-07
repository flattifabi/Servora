<#
.SYNOPSIS
    Extracts the release notes of one version from CHANGELOG.md (Keep a Changelog format).

.DESCRIPTION
    Returns the section "## [<version>]" up to the next "## " heading. When the version has no section yet, the
    "## [Unreleased]" section is used. Installation and checksum hints are appended.

.PARAMETER Version
    The version, for example 1.2.0 (a leading "v" is ignored).

.PARAMETER ChangelogPath
    Path of the changelog. Default: <repo>/CHANGELOG.md.

.PARAMETER OutFile
    Optional output file. Without it the notes are written to the pipeline.

.EXAMPLE
    ./build/Get-ReleaseNotes.ps1 -Version 1.2.0 -OutFile artifacts/release-notes.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [string] $ChangelogPath,
    [string] $OutFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Version = $Version.TrimStart('v')
if (-not $ChangelogPath) {
    $ChangelogPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'CHANGELOG.md'
}

function Get-Section([string[]] $Lines, [string] $HeadingPattern) {
    $collecting = $false
    $section = New-Object System.Collections.Generic.List[string]
    foreach ($line in $Lines) {
        if ($line -match '^## ') {
            if ($collecting) { break }
            if ($line -match $HeadingPattern) { $collecting = $true; continue }
        }
        elseif ($collecting) {
            $section.Add($line)
        }
    }
    if ($collecting) { return (($section -join "`n").Trim()) }
    return $null
}

$lines = if (Test-Path $ChangelogPath) { Get-Content -Path $ChangelogPath } else { @() }
$escaped = [regex]::Escape($Version)
$body = Get-Section $lines "^## \[?v?$escaped\]?(\s|$)"
if (-not $body) {
    $unreleased = Get-Section $lines '^## \[?Unreleased\]?'
    $body = if ($unreleased) { $unreleased } else { 'See CHANGELOG.md for details.' }
}

# Links in a GitHub release page are not resolved relative to the repository, so point them at the tagged sources.
$docs = if ($env:GITHUB_REPOSITORY) {
    "$($env:GITHUB_SERVER_URL)/$($env:GITHUB_REPOSITORY)/blob/v$Version/docs/operations"
}
else {
    'docs/operations'
}

$notes = @"
$body

---

### Installation

Download ``ServoraSetup-$Version-win-x64.exe`` and run it as administrator. It installs Servora as the Windows service
``Servora`` or updates an existing installation in place (backup, health check, automatic rollback).
Unattended: ``ServoraSetup-$Version-win-x64.exe --silent``. Manual installation: ``Servora-$Version-win-x64.zip``.
See the [installation guide]($docs/installation.md) and the [upgrade guide]($docs/upgrade.md).

Verify the downloads with ``SHA256SUMS.txt``:
``Get-FileHash -Algorithm SHA256 .\ServoraSetup-$Version-win-x64.exe``
"@

if ($OutFile) {
    $directory = Split-Path -Parent $OutFile
    if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
    [System.IO.File]::WriteAllText($OutFile, $notes + "`n", (New-Object System.Text.UTF8Encoding($false)))
}
else {
    $notes
}
