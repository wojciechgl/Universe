#requires -version 4
[CmdletBinding(PositionalBinding = $false)]
param(
    [switch]$Help,

    # Parameters
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('win-x64', 'win-x86')]
    [string]$Platform = $null,

    # Build phase
    [switch]$BuildRuntimeStore,
    [switch]$BuildPackageArchive,

    # Logging
    [switch]$BinaryLog,

    # Catch-all
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$badargs
)

function Write-Usage() {
    Write-Host @"
Usage: build.ps1 [options]

Options:
  -c, -Configuration <CONFIG>       Build configuration, Debug or Release. [Debug]
  -p, -Platform <PLATFORM>          Target platform, win-x64 or win-x86. [Auto detected based on machine arch]
"@
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 1

if ($Help) {
    Write-Usage
    exit 2
}

if ($badargs) {
    Write-Host -f Red "Unrecognized arguments: $badargs"
    Write-Host "Execute with -help to see usage."
    exit 1
}

if (-not $Platform) {
    if ($env:PROCESSOR_ARCHITEW6432 -eq 'AMD64' -or $env:PROCESSOR_ARCHITECTURE -eq 'AMD64') {
        $Platform = 'win-x64'
    } elseif ($env:PROCESSOR_ARCHITECTURE -eq 'x86') {
        $Platform = 'win-x86'
    } else {
        Write-Host -f Red "Could not detect current machine architecture. A value for -Platform is required."
        Write-Host "Execute with -help to see usage."
        exit 1
    }
    Write-Host -f Yellow "Auto-detected machine architecture as $Platform"
}

$logDir = "$PSScriptRoot/artifacts/logs"
mkdir $logDir -ErrorAction Ignore

$misc = @()
$targets = @()
$properties = @('-property:GenerateFullPaths=true', "-property:Platform=$Platform", "-property:Configuration=$Configuration")

$count = 0

if ($BuildRuntimeStore) {
    $count +=1
    $targets += '-target:src\RuntimeStore\RuntimeStore'
    $properties += '-property:BuildProjectReferences=false'
}

if ($BuildPackageArchive) {
    $count +=1
    $targets += '-target:src\PackageArchive\PackageArchive'
    $properties += '-property:BuildProjectReferences=false'
}

if ($count -eq 0) {
    $targets += '-target:Build'
}

if ($count -gt 1) {
    Write-Host -f Red "At the moment, only building once phase at a time is supported."
    exit 1
}

if ($BinaryLog) {
    $misc += "-binaryLogger:$logDir/msbuild.binlog"
}

if ($VerbosePreference) {
    $misc += '-verbosity:normal'
}

& dotnet msbuild `
    "$PSScriptRoot/Microsoft.AspNetCore.sln" `
    '-clp:Summary' `
    '-noautoresponse' `
    '-maxcpucount' `
    '-consoleLoggerParameters:Summary;ShowCommandLine' `
    @targets @properties @misc
