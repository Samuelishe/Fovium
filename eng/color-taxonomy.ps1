#requires -Version 7.0

[CmdletBinding(DefaultParameterSetName = 'Full')]
param(
    [string]$OutputDirectory,
    [switch]$Deep,
    [string]$ResearchReport,
    [string]$ReferenceDirectory,
    [switch]$Open,
    [switch]$Png,
    [Parameter(ParameterSetName = 'Static')]
    [switch]$StaticOnly,
    [Parameter(ParameterSetName = 'Html')]
    [switch]$HtmlOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Png -and $HtmlOnly) {
    throw '-Png requires static report output and cannot be combined with -HtmlOnly.'
}
if ($Open -and $StaticOnly) {
    throw '-Open requires index.html and cannot be combined with -StaticOnly.'
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'Fovium.Tools.ColorTaxonomyAudit/Fovium.Tools.ColorTaxonomyAudit.csproj'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts/reports/color-semantics'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

if (-not [string]::IsNullOrWhiteSpace($ResearchReport) -and -not [System.IO.Path]::IsPathRooted($ResearchReport)) {
    $ResearchReport = Join-Path $repositoryRoot $ResearchReport
}

if ($Deep -and [string]::IsNullOrWhiteSpace($ResearchReport)) {
    $auditDirectory = Join-Path $repositoryRoot 'artifacts/color-taxonomy-audit/report-enrichment'
    $auditArguments = @(
        'run', '--project', $projectPath, '--configuration', 'Release', '--',
        '--mode', 'deep', '--output', $auditDirectory
    )
    if ([string]::IsNullOrWhiteSpace($ReferenceDirectory)) {
        $defaultReferences = Join-Path $repositoryRoot 'artifacts/color-taxonomy-audit/references'
        if (Test-Path -LiteralPath $defaultReferences -PathType Container) {
            $ReferenceDirectory = $defaultReferences
        }
    }
    elseif (-not [System.IO.Path]::IsPathRooted($ReferenceDirectory)) {
        $ReferenceDirectory = Join-Path $repositoryRoot $ReferenceDirectory
    }

    if (-not [string]::IsNullOrWhiteSpace($ReferenceDirectory)) {
        $auditArguments += @('--references', $ReferenceDirectory)
    }

    & dotnet @auditArguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $ResearchReport = Join-Path $auditDirectory 'summary.json'
}

$commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) {
    $commit = 'unknown'
}

$reportArguments = @(
    'run', '--project', $projectPath, '--configuration', 'Release', '--',
    'report', '--output', $OutputDirectory, '--commit', $commit
)
if (-not [string]::IsNullOrWhiteSpace($ResearchReport)) {
    $reportArguments += @('--research-report', $ResearchReport)
}
if ($StaticOnly) {
    $reportArguments += '--static-only'
}
elseif ($HtmlOnly) {
    $reportArguments += '--html-only'
}

& dotnet @reportArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($Png) {
    $chromeCandidates = @(
        (Join-Path ${env:ProgramFiles} 'Google/Chrome/Application/chrome.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Google/Chrome/Application/chrome.exe'),
        (Join-Path $env:LOCALAPPDATA 'Google/Chrome/Application/chrome.exe')
    )
    $chrome = $chromeCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($chrome)) {
        throw 'PNG rendering requested, but Google Chrome was not found.'
    }

    $staticDirectory = Join-Path $OutputDirectory 'static'
    $chromeProfile = Join-Path ([System.IO.Path]::GetTempPath()) ("fovium-color-report-" + [Guid]::NewGuid().ToString('N'))
    $svgTargets = @(
        (Join-Path $staticDirectory 'overview.svg'),
        (Join-Path $staticDirectory 'relations.svg')
    ) + @(Get-ChildItem -LiteralPath (Join-Path $staticDirectory 'domains') -Filter '*.svg' -File |
        Sort-Object Name | Select-Object -ExpandProperty FullName)
    foreach ($svg in $svgTargets) {
        $svgText = Get-Content -Raw -LiteralPath $svg
        if ($svgText -notmatch '<svg[^>]+width="(?<width>\d+)"[^>]+height="(?<height>\d+)"') {
            throw "Cannot determine SVG dimensions for $svg."
        }

        $pngPath = [System.IO.Path]::ChangeExtension($svg, '.png')
        $uri = [System.Uri]::new($svg).AbsoluteUri
        $process = Start-Process -FilePath $chrome -ArgumentList @(
            '--headless=new',
            '--disable-gpu',
            '--hide-scrollbars',
            "--user-data-dir=$chromeProfile",
            "--window-size=$($Matches.width),$($Matches.height)",
            "--screenshot=$pngPath",
            $uri
        ) -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) {
            throw "Chrome failed to render $svg."
        }
    }

    $resolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $resolvedProfile = [System.IO.Path]::GetFullPath($chromeProfile)
    if ($resolvedProfile.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedProfile).StartsWith('fovium-color-report-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$indexPath = Join-Path $OutputDirectory 'index.html'
if ($Open) {
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        throw 'The selected report mode did not generate index.html.'
    }

    Start-Process -FilePath $indexPath
}

Write-Output "Color Semantics report generated: $OutputDirectory"
