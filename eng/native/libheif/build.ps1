[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Rid = 'win-x64',

    [string]$HeifFixture,

    [string]$AvifFixture
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = $PSScriptRoot
$repositoryRoot = (Resolve-Path (Join-Path $scriptRoot '../../..')).Path
$versions = Get-Content -Raw -LiteralPath (Join-Path $scriptRoot 'versions.json') | ConvertFrom-Json

if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -notin @('AMD64', 'x86_64')) {
    throw "win-x64 build requires an x64 Windows host; found $($env:PROCESSOR_ARCHITECTURE)."
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio vswhere.exe was not found.'
}

$visualStudio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) {
    throw 'A Visual Studio installation with the x64 C/C++ toolchain was not found.'
}

$developerCommand = Join-Path $visualStudio 'Common7/Tools/VsDevCmd.bat'
$environmentLines = & cmd.exe /d /s /c "`"$developerCommand`" -no_logo -arch=x64 -host_arch=x64 && set"
if ($LASTEXITCODE -ne 0) {
    throw "VsDevCmd.bat failed with exit code $LASTEXITCODE."
}

foreach ($line in $environmentLines) {
    $separator = $line.IndexOf('=')
    if ($separator -le 0) {
        continue
    }

    $name = $line.Substring(0, $separator)
    $value = $line.Substring($separator + 1)
    Set-Item -Path "Env:$name" -Value $value
}

foreach ($tool in @('cmake', 'nasm', 'python')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Required build tool '$tool' is not available."
    }
}

$toolingRoot = Join-Path $repositoryRoot "artifacts/native/tooling/$Rid"
$venvPython = Join-Path $toolingRoot 'Scripts/python.exe'
if (-not (Test-Path -LiteralPath $venvPython)) {
    python -m venv $toolingRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Python virtual environment creation failed with exit code $LASTEXITCODE."
    }
}

& $venvPython -m pip install --disable-pip-version-check --no-input `
    "meson==$($versions.tooling.meson)" `
    "ninja==$($versions.tooling.ninja)"
if ($LASTEXITCODE -ne 0) {
    throw "Pinned Meson/Ninja installation failed with exit code $LASTEXITCODE."
}

$env:PATH = "$(Join-Path $toolingRoot 'Scripts');$env:PATH"
$arguments = @((Join-Path $scriptRoot 'build.py'), '--rid', $Rid)
if ($HeifFixture) {
    $arguments += @('--heif-fixture', $HeifFixture)
}
if ($AvifFixture) {
    $arguments += @('--avif-fixture', $AvifFixture)
}

& $venvPython @arguments
exit $LASTEXITCODE
