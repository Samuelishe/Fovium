#requires -Version 7.0

[CmdletBinding()]
param(
    [ValidateRange(1, 1000)]
    [int]$Top = 25
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'Fovium.Tools.ProjectStats/Fovium.Tools.ProjectStats.csproj'
$outputPath = Join-Path $repositoryRoot 'project-stats.md'

& dotnet run --project $projectPath -- $repositoryRoot --top $Top --markdown --output $outputPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Output "ProjectStats report generated: $outputPath"
