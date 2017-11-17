#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Updates the ExternalDependencies to reflect what's on the source.
.PARAMETER GitAuthorName
    The author name to use in the commit message. (Optional)
.PARAMETER GitAuthorEmail
    The author email to use in the commit message. (Optional)
.PARAMETER GitCommitArgs
    Additional arguments to pass into git-commit
.PARAMETER NoCommit
    Make changes without executing git-commit
.PARAMETER Force
    Specified this to make a commit with any changes
#>
[cmdletbinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$DependencySource,
    [string]$GitAuthorName = $null,
    [string]$GitAuthorEmail = $null,
    [string[]]$GitCommitArgs = @(),
    [switch]$NoCommit,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

Import-Module "$PSScriptRoot/common.psm1" -Scope Local -Force

# Gets a list of the ExternalDependencies we should look for from the given file.
function _get_dependencies(
    [Parameter(Mandatory = $true)]
    [string]$dependencyFile
) {
    $dependencies = New-Object System.Collections.ArrayList

    foreach($line in Get-Content $dependencyFile)
    {
        if($linst.startswith("<ExternalDependency"))
        {

        }
    }
}

function Get-NugetSource(
    [Parameter(Mandatory = $true)]
    [string]$sourceLocation
) {
    _get_dependencies

    Find-Package ""

    throw (New-Object System.NotImplementedException)
}

function Get-Source(
    [Parameter(Mandatory = $true)]
    [string]$sourceLocation
) {
    if ($sourceLocation.endswith("index.json")) {
        return Get-NugetSource $sourceLocation
    }
    else {
        throw (New-Object System.NotImplementedException)
    }
}

function Update-DependencyFile(
    [Parameter(Mandatory = $true)]
    [HashTable]$dependencies
) {
    throw (New-Object System.NotImplementedException)
}

$RepoRoot = Resolve-Path "$PSScriptRoot\.."
$depsFile = "build/dependencies.props"

Push-Location $RepoRoot | Out-Null
try {
    $dependencies = Get-Source $DependencySource

    Update-DependencyFile $dependencies $depsFile

    & .\run.ps1 default-build

    if ($LASTEXITCODE -ne 0) {
        throw "Build of Universe failed."
    }

    Invoke-Block { & git @gitConfigArgs add $depsFile }
    & git diff --cached --quiet ./
    if ($LASTEXITCODE -ne 0) {
        Invoke-Block { & git @gitConfigArgs commit --quiet -m "Update dependencies.props`n`n[auto-updated: dependencies]" @GitCommitArgs }

        if (-not $NoPush -and ($Force -or ($PSCmdlet.ShouldContinue($shortMessage, 'Push the changes to Universes?')))) {
            Invoke-Block { & git @gitConfigArgs push origin HEAD:dev}
        }
    }
    else {
        Write-Host "No changes in External dependencies."
    }
}
finally {
    Pop-Location
}
