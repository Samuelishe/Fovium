[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string[]] $Root,

    [ValidateRange(1, 500)]
    [int] $SampleCount = 60,

    [ValidateNotNullOrEmpty()]
    [string] $Seed = 'photo-color-audit-v1',

    [ValidateRange(0, 500)]
    [int] $TuningCount = 25,

    [ValidateRange(0, 500)]
    [int] $ValidationCount = 15,

    [ValidateSet('Tuning', 'Validation', 'Holdout', 'All')]
    [string] $Split = 'Tuning',

    [ValidateNotNullOrEmpty()]
    [string] $OutputDirectory = 'artifacts/reports/photo-color-audit',

    [ValidateSet(96, 128, 160, 192)]
    [int[]] $Resolution = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

if (($TuningCount + $ValidationCount) -gt $SampleCount) {
    throw 'TuningCount + ValidationCount must not exceed SampleCount.'
}

$resolvedRoots = @($Root | ForEach-Object {
        $resolved = [System.IO.Path]::GetFullPath($_)
        if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
            throw "Photo root does not exist: $resolved"
        }

        $resolved
    })
if ($resolvedRoots.Count -ne @($resolvedRoots | Select-Object -Unique).Count) {
    throw 'Photo roots must be unique.'
}

$extensions = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($extension in '.jpg', '.jpeg', '.png', '.webp', '.tif', '.tiff', '.bmp') {
    [void] $extensions.Add($extension)
}

function Get-StableHash {
    param([Parameter(Mandatory)][string] $Value)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return [System.Convert]::ToHexString($hash)
}

$baseQuota = [Math]::Floor($SampleCount / $resolvedRoots.Count)
$remainder = $SampleCount % $resolvedRoots.Count
$selected = [System.Collections.Generic.List[object]]::new()
for ($rootIndex = 0; $rootIndex -lt $resolvedRoots.Count; $rootIndex++) {
    $rootPath = $resolvedRoots[$rootIndex]
    $rootIdentifier = 'root-{0:D2}' -f ($rootIndex + 1)
    $quota = $baseQuota + $(if ($rootIndex -lt $remainder) { 1 } else { 0 })
    $candidates = @(Get-ChildItem -LiteralPath $rootPath -File -Recurse | Where-Object {
            $_.Length -ge 204800 -and $extensions.Contains($_.Extension)
        } | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($rootPath, $_.FullName).Replace('\', '/')
            [pscustomobject]@{
                Path       = $_.FullName
                Identifier = "$rootIdentifier/$relativePath"
                Hash       = Get-StableHash "$Seed`n$rootIdentifier`n$relativePath"
            }
        } | Sort-Object Hash, Identifier)
    if ($candidates.Count -lt $quota) {
        throw "Root $rootIdentifier contains only $($candidates.Count) eligible images; $quota required."
    }

    foreach ($candidate in $candidates | Select-Object -First $quota) {
        $selected.Add($candidate)
    }
}

$ordered = @($selected | Sort-Object Hash, Identifier)
$splits = [ordered]@{
    Tuning = @($ordered | Select-Object -First $TuningCount)
    Validation = @($ordered | Select-Object -Skip $TuningCount -First $ValidationCount)
    Holdout = @($ordered | Select-Object -Skip ($TuningCount + $ValidationCount))
}
$requestedSplits = if ($Split -eq 'All') { @('Tuning', 'Validation', 'Holdout') } else { @($Split) }

foreach ($splitName in $requestedSplits) {
    $items = @($splits[$splitName])
    if ($items.Count -eq 0) {
        Write-Host "Skipping empty $splitName split."
        continue
    }

    $splitOutput = Join-Path $outputRoot $splitName.ToLowerInvariant()
    $env:FOVIUM_PHOTO_COLOR_PROFILE_IMAGES = $items.Path -join [System.IO.Path]::PathSeparator
    $env:FOVIUM_PHOTO_COLOR_PROFILE_IDENTIFIERS = $items.Identifier -join [System.IO.Path]::PathSeparator
    $env:FOVIUM_PHOTO_COLOR_PROFILE_OUTPUT = $splitOutput
    $env:FOVIUM_PHOTO_COLOR_PROFILE_RESOLUTIONS = $Resolution -join ','
    try {
        & dotnet test (Join-Path $repositoryRoot 'Fovium.Tests/Fovium.Tests.csproj') `
            -c Release `
            --filter 'FullyQualifiedName~PhotoColorProfilePerformanceSmokeTests.OptInRealPhotosReportProjectionCostAndRenderContactSheet'
        if ($LASTEXITCODE -ne 0) {
            throw "Photo Color Profile audit failed for $splitName."
        }
    }
    finally {
        Remove-Item Env:FOVIUM_PHOTO_COLOR_PROFILE_IMAGES -ErrorAction SilentlyContinue
        Remove-Item Env:FOVIUM_PHOTO_COLOR_PROFILE_IDENTIFIERS -ErrorAction SilentlyContinue
        Remove-Item Env:FOVIUM_PHOTO_COLOR_PROFILE_OUTPUT -ErrorAction SilentlyContinue
        Remove-Item Env:FOVIUM_PHOTO_COLOR_PROFILE_RESOLUTIONS -ErrorAction SilentlyContinue
    }
}

[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$manifest = [ordered]@{
    ContractVersion = 1
    Seed = $Seed
    SampleCount = $SampleCount
    RootCount = $resolvedRoots.Count
    Selection = 'Per-root SHA-256 quota, followed by global SHA-256 order.'
    MinimumEncodedBytes = 204800
    Splits = [ordered]@{
        Tuning = @($splits.Tuning | ForEach-Object Identifier)
        Validation = @($splits.Validation | ForEach-Object Identifier)
        Holdout = @($splits.Holdout | ForEach-Object Identifier)
    }
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputRoot 'selection-manifest.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $outputRoot '.fovium-photo-color-audit') -Value 'contract-version=1' -Encoding ascii
Write-Host "Audit evidence: $outputRoot"
