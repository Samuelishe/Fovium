[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateSet('Safe', 'Reports', 'NativeBuilds', 'AllGenerated')]
    [string[]] $Mode = @('Safe'),

    [switch] $IncludeCaches
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$artifactPrefix = $artifactRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
    Write-Host "Artifacts directory does not exist: $artifactRoot"
    return
}

$targets = [System.Collections.Generic.List[object]]::new()

function Add-ArtifactTarget {
    param(
        [Parameter(Mandatory)]
        [string] $RelativePath,

        [Parameter(Mandatory)]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Purpose
    )

    $target = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $RelativePath))
    if (-not $target.StartsWith($artifactPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing target outside the artifacts directory: $target"
    }

    if ($target -eq $artifactRoot) {
        throw 'Refusing to target the artifacts root itself.'
    }

    if (Test-Path -LiteralPath $target) {
        $targets.Add([pscustomobject]@{
                Path     = $target
                Relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $target)
                Category = $Category
                Purpose  = $Purpose
            })
    }
}

function Add-KnownChildren {
    param(
        [Parameter(Mandatory)]
        [string] $ParentRelativePath,

        [Parameter(Mandatory)]
        [string[]] $Names,

        [Parameter(Mandatory)]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Purpose
    )

    foreach ($name in $Names) {
        Add-ArtifactTarget (Join-Path $ParentRelativePath $name) $Category $Purpose
    }
}

function Add-MatchingKnownChildren {
    param(
        [Parameter(Mandatory)]
        [string] $ParentRelativePath,

        [Parameter(Mandatory)]
        [string] $NamePattern,

        [Parameter(Mandatory)]
        [string] $Category,

        [Parameter(Mandatory)]
        [string] $Purpose
    )

    $parent = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ParentRelativePath))
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        return
    }

    foreach ($child in Get-ChildItem -LiteralPath $parent -Force) {
        if ($child.Name -match $NamePattern) {
            Add-ArtifactTarget (Join-Path $ParentRelativePath $child.Name) $Category $Purpose
        }
    }
}

$selectedModes = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($selectedMode in $Mode) {
    [void] $selectedModes.Add($selectedMode)
}

if ($selectedModes.Contains('AllGenerated')) {
    [void] $selectedModes.Add('Safe')
    [void] $selectedModes.Add('Reports')
    [void] $selectedModes.Add('NativeBuilds')
}

if ($selectedModes.Contains('Safe')) {
    Add-KnownChildren 'artifacts' @(
        'color-semantics-baseline',
        'r10-b-evidence',
        'r10d',
        'find-untested-before.json'
    ) 'Safe' 'Stale, reproducible stage evidence or developer report'

    Add-MatchingKnownChildren `
        'artifacts\color-taxonomy-audit' `
        '^(?:f\d|bench-|chrome-profile|example-evaluation$|report-enrichment$|work-after$)' `
        'Safe' `
        'Generated taxonomy audit, browser profile, benchmark, or review output'

    Add-KnownChildren 'artifacts\r10e' @(
        'baseline',
        'chrome-dom-profile',
        'chrome-profile',
        'chrome-smoke-profile-2',
        'cross-platform',
        'diff',
        'discovery',
        'enriched-preproduction',
        'explorer-smoke.png',
        'final',
        'final-candidate',
        'final-diff',
        'final-fast',
        'final-fast-core',
        'final-tuned',
        'final-tuned-fast-core'
    ) 'Safe' 'Generated R10-E report, diff, browser profile, or screenshot'
}

if ($selectedModes.Contains('Reports')) {
    Add-MatchingKnownChildren `
        'artifacts\reports' `
        '^(?:color-semantics(?:-|$)|photo-color-profile-)' `
        'Reports' `
        'Reproducible generated report or contact sheet'
}

if ($selectedModes.Contains('NativeBuilds')) {
    Add-KnownChildren 'artifacts\native' @('work', 'tooling') `
        'NativeBuilds' `
        'Reproducible native source, object, install, or build-tool environment'
    Add-MatchingKnownChildren `
        'artifacts\native' `
        '^fovium-(?:libheif|lcms2)-.+$' `
        'NativeBuilds' `
        'Reproducible native runtime bundle; verified packages remain cached'
}

if ($IncludeCaches) {
    Add-KnownChildren 'artifacts\native' @('downloads', 'packages') `
        'Caches' `
        'Pinned native download or verified package cache (explicit opt-in)'
    Add-KnownChildren 'artifacts\color-taxonomy-audit' @(
        'references',
        'references-f12',
        'references-fresh'
    ) 'Caches' 'Pinned color-research reference cache (explicit opt-in)'
    Add-KnownChildren 'artifacts\r10e' @('references') `
        'Caches' `
        'Pinned R10-E research reference cache (explicit opt-in)'
}

$uniqueTargets = @(
    $targets |
        Sort-Object Path -Unique |
        Sort-Object Relative
)

if ($uniqueTargets.Count -eq 0) {
    Write-Host 'No known generated artifact targets matched the selected policy.'
    return
}

$plannedBytes = 0L
$plannedFiles = 0L
$plannedDirectories = 0L
foreach ($target in $uniqueTargets) {
    $item = Get-Item -LiteralPath $target.Path -Force
    if ($item.PSIsContainer) {
        $files = @(Get-ChildItem -LiteralPath $target.Path -File -Force -Recurse)
        $directories = @(Get-ChildItem -LiteralPath $target.Path -Directory -Force -Recurse)
        $bytes = [long] (($files | Measure-Object -Property Length -Sum).Sum ?? 0)
        $fileCount = [long] $files.Count
        $directoryCount = [long] (1 + $directories.Count)
    }
    else {
        $bytes = [long] $item.Length
        $fileCount = 1L
        $directoryCount = 0L
    }

    $plannedBytes += $bytes
    $plannedFiles += $fileCount
    $plannedDirectories += $directoryCount
    Write-Host ('[{0}] {1} ({2:N0} bytes, {3:N0} files, {4:N0} directories) - {5}' -f $target.Category, $target.Relative, $bytes, $fileCount, $directoryCount, $target.Purpose)
}

Write-Host ('Planned: {0:N0} bytes, {1:N0} files, {2:N0} directories across {3:N0} allowlisted targets.' -f $plannedBytes, $plannedFiles, $plannedDirectories, $uniqueTargets.Count)

foreach ($target in $uniqueTargets) {
    if ($PSCmdlet.ShouldProcess($target.Path, "Remove allowlisted $($target.Category) artifact")) {
        Remove-Item -LiteralPath $target.Path -Force -Recurse
    }
}
