#requires -Version 7.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required but was not found on PATH.'
}

$repositoryRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $repositoryRoot) {
    throw 'The current directory is not inside a Git repository.'
}

$repositoryRoot = $repositoryRoot.Trim()
$branch = (& git symbolic-ref --quiet --short HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $branch) {
    $branch = '(detached or unavailable)'
}

$head = (& git rev-parse --verify HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $head) {
    $head = '(unborn; no commit)'
}

$gitDirectory = (& git -C $repositoryRoot rev-parse --path-format=absolute --git-dir).Trim()
$operations = [System.Collections.Generic.List[string]]::new()
$operationMarkers = [ordered]@{
    Merge      = 'MERGE_HEAD'
    CherryPick = 'CHERRY_PICK_HEAD'
    Revert     = 'REVERT_HEAD'
    Bisect     = 'BISECT_LOG'
    Rebase     = 'rebase-merge'
    RebaseApply = 'rebase-apply'
}

foreach ($entry in $operationMarkers.GetEnumerator()) {
    if (Test-Path -LiteralPath (Join-Path $gitDirectory $entry.Value)) {
        $operations.Add($entry.Key)
    }
}

Write-Output "Repository root: $repositoryRoot"
Write-Output "Current branch: $branch"
Write-Output "HEAD: $head"
Write-Output "Current operation: $(if ($operations.Count) { $operations -join ', ' } else { 'none' })"
Write-Output 'Short status:'
$status = @(& git -C $repositoryRoot status --short)
if ($status.Count) {
    $status | Write-Output
} else {
    Write-Output '(clean)'
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Output 'dotnet: unavailable'
    return
}

$dotnetVersion = (& dotnet --version).Trim()
Write-Output "dotnet version: $dotnetVersion"
Write-Output 'Installed SDKs:'
$sdks = @(& dotnet --list-sdks)
if ($sdks.Count) {
    $sdks | Write-Output
} else {
    Write-Output '(none reported)'
}
