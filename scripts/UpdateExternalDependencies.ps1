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

$depsFile = "build/dependencies.props"

# Gets a list of the ExternalDependencies we should look for from the given file.
function _get_dependenciesProp(
    [Parameter(Mandatory = $true)]
    [string]$dependencyFile
) {
    $depProp = @{}
    $depProp.Dependencies = New-Object System.Collections.ArrayList
    $depProp.Variables = New-Object System.Collections.Hashtable

    [xml]$depsXml = Get-Content $dependencyFile

    foreach ($propGroup in $depsXml.Project.PropertyGroup) {
        foreach ($element in $propGroup.ChildNodes) {
            $depProp.Variables[$element.Name] = $element.InnerText
        }
    }

    foreach ($itemGroup in $depsXml.Project.ItemGroup) {
        foreach ($extDep in $itemGroup.ExternalDependency) {
            $dependency = @{}

            $dependency.Package = $extDep.Include
            $dependency.Version = $extDep.Version
            if ($extDep.HasAttribute("Mirror")) {
                $dependency.Mirror = $extDep.Mirror
            }
            if ($extDep.HasAttribute("Private")) {
                $dependency.Private = $extDep.Private
            }

            $source = $extDep.Source

            if ($source.startswith("`$(")) {
                $trimmed = $source.TrimStart('$', '(')
                $trimmed = $trimmed.TrimEnd(')')
                $dependency.Source = $depProp.Variables[$trimmed]
            }

            [void]$depProp.Dependencies.Add($dependency)
        }
    }

    return $depProp
}

function Get-LatestNugetVersions(
    [Parameter(Mandatory = $true)]
    [string]$sourceLocation,
    [Parameter(Mandatory = $true)]
    [System.Collections.Hashtable]$dependencies
) {
    $nugetSource = Invoke-WebRequest -Uri $sourceLocation | ConvertFrom-Json

    $root = $nugetSource| ForEach-Object { $nugetSource.Resources } | Where-Object { $_.'@type' -eq "PackageBaseAddress/3.0.0" } | ForEach-Object { $_.'@id' }

    if ($root -eq $null) {
        throw "Source '$nugetSource' doesn't support PackageBaseAddress/3.0.0"
    }

    $latest = New-Object System.Collections.Hashtable

    foreach ($dep in $dependencies) {
        try {
            $response = Invoke-WebRequest -Uri ($root + $dep.ToLowerInvariant() + "/index.json")

            $versions = @(ConvertFrom-Json -InputObject $response `
                | ForEach-Object {$_.versions} `
                | Sort-Object)

            if ($versions.Count -eq 0) {
                Write-Warning "$sourceLocation has no versions of $dep."
            }
            else {
                Write-Host "Choosing version: $($versions[-1])"
                $latest.Add($dep, $versions[-1])
            }
        }
        catch {
            Write-Warning "$sourceLocation doesn't have $dep"
        }
    }

    return $latest
}

function Get-LatestVersions(
    [Parameter(Mandatory = $true)]
    [string]$sourceLocation
) {
    $depsProps = _get_dependenciesProp $depsFile

    if ($sourceLocation.endswith("v3/index.json")) {
        return Get-LatestNugetVersions $sourceLocation $depsProps
    }
    else {
        throw (New-Object System.NotImplementedException)
    }
}

function Update-DependencyFile(
    [Parameter(Mandatory = $true)]
    [Object]$dependencies
) {
    foreach ($dep in $dependencies) {
        Write-Host "$dep"
    }

    throw (New-Object System.NotImplementedException)
}

$RepoRoot = Resolve-Path "$PSScriptRoot\.."

Push-Location $RepoRoot | Out-Null
try {
    $dependencies = Get-LatestVersions $DependencySource

    Update-DependencyFile $dependencies

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
