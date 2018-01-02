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
    [switch]$BuildMetapackage,

    [switch]$NoDependencies,

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
  -noDependencies                   Do not build ProjectReference's

Targets:
  -buildRuntimeStore                Builds the ASP.NET Core runtime store
  -buildPackageArchive              Builds the LZMA archive
  -buildMetapackage                 Builds the Microsoft.AspNetCore.All metapackage
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

if ($NoDependencies) {
    $properties += '-property:BuildProjectReferences=false'
}

if ($BuildRuntimeStore) {
    $targets += '-target:src\RuntimeStore\RuntimeStore'
}

if ($BuildPackageArchive) {
    $targets += '-target:src\PackageArchive\PackageArchive'
}

if ($BuildMetapackage) {
    $targets += '-target:src\Metapackage\Metapackage'
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
