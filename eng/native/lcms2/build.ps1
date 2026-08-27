[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = $PSScriptRoot
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
    if ($separator -gt 0) {
        Set-Item -Path "Env:$($line.Substring(0, $separator))" -Value $line.Substring($separator + 1)
    }
}

foreach ($tool in @('cmake', 'python', 'cl', 'dumpbin')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "Required build tool '$tool' is not available."
    }
}

& python (Join-Path $scriptRoot 'build.py') --rid $Rid
exit $LASTEXITCODE
