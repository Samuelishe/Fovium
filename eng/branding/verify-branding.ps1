[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$brandingRoot = Join-Path $repositoryRoot 'resources/branding'
$iconPath = Join-Path $brandingRoot 'fovium.ico'
$projectPath = Join-Path $repositoryRoot 'Fovium/Fovium.csproj'
$evidenceRoot = Join-Path $repositoryRoot 'artifacts/branding'
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null

function Assert-True {
    param([bool] $Condition, [string] $Message)

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-Checked {
    param([string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-IcoFrames {
    param([string] $Path)

    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        Assert-True ($reader.ReadUInt16() -eq 0) 'ICO reserved field is invalid.'
        Assert-True ($reader.ReadUInt16() -eq 1) 'ICO type is not icon.'
        $count = $reader.ReadUInt16()
        Assert-True ($count -gt 0) 'ICO contains no images.'
        $frames = @()
        for ($index = 0; $index -lt $count; $index++) {
            $widthByte = $reader.ReadByte()
            $heightByte = $reader.ReadByte()
            $null = $reader.ReadByte()
            $null = $reader.ReadByte()
            $planes = $reader.ReadUInt16()
            $bitsPerPixel = $reader.ReadUInt16()
            $length = $reader.ReadUInt32()
            $offset = $reader.ReadUInt32()
            $frames += [pscustomobject]@{
                Width = if ($widthByte -eq 0) { 256 } else { [int]$widthByte }
                Height = if ($heightByte -eq 0) { 256 } else { [int]$heightByte }
                Planes = $planes
                BitsPerPixel = $bitsPerPixel
                Length = $length
                Offset = $offset
            }
        }

        foreach ($frame in $frames) {
            Assert-True ($frame.Width -eq $frame.Height) "ICO frame $($frame.Width)x$($frame.Height) is not square."
            Assert-True ($frame.Planes -eq 1 -and $frame.BitsPerPixel -eq 32) "ICO frame $($frame.Width) has invalid pixel metadata."
            Assert-True (($frame.Offset + $frame.Length) -le $stream.Length) "ICO frame $($frame.Width) exceeds the file."
            $stream.Position = $frame.Offset
            $signature = $reader.ReadBytes(8)
            $pngSignature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
            Assert-True ([Linq.Enumerable]::SequenceEqual($signature, $pngSignature)) "ICO frame $($frame.Width) is not PNG encoded."
        }
        return $frames
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Test-ReadmeLinks {
    $readmePath = Join-Path $repositoryRoot 'README.md'
    $readme = [IO.File]::ReadAllText($readmePath)
    $targets = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($match in [regex]::Matches($readme, '!?(?:\[[^\]]*\])\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim().Trim('<', '>')
        $null = $targets.Add($target)
    }
    foreach ($match in [regex]::Matches($readme, '<img\s+[^>]*src=["'']([^"'']+)["'']', 'IgnoreCase')) {
        $null = $targets.Add($match.Groups[1].Value)
    }

    foreach ($target in $targets) {
        if ($target.StartsWith('#') -or [Uri]::IsWellFormedUriString($target, [UriKind]::Absolute)) {
            continue
        }
        $relative = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($relative)) {
            continue
        }
        $decoded = [Uri]::UnescapeDataString($relative).Replace('/', [IO.Path]::DirectorySeparatorChar)
        $resolved = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $decoded))
        Assert-True ($resolved.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) "README target escapes the repository: $target"
        Assert-True (Test-Path -LiteralPath $resolved) "README target does not exist: $target"
    }

    $workflowBadges = @(
        'actions/workflows/ci.yml/badge.svg?branch=master',
        'actions/workflows/native-libheif.yml/badge.svg?branch=master',
        'actions/workflows/native-lcms2.yml/badge.svg?branch=master'
    )
    foreach ($badge in $workflowBadges) {
        Assert-True ($readme.Contains($badge, [StringComparison]::Ordinal)) "README workflow badge is missing: $badge"
    }
}

Assert-True (Test-Path -LiteralPath $iconPath -PathType Leaf) 'Canonical Windows icon is missing.'
$frames = Get-IcoFrames $iconPath
$expectedSizes = @(16, 20, 24, 32, 48, 64, 128, 256)
Assert-True ([Linq.Enumerable]::SequenceEqual([int[]]$frames.Width, [int[]]$expectedSizes)) "ICO sizes are not the expected ordered set: $($frames.Width -join ', ')."

[xml]$project = [IO.File]::ReadAllText($projectPath)
$applicationIcon = [string]$project.Project.PropertyGroup.ApplicationIcon
$includeWindowIcon = [string]$project.Project.PropertyGroup.AvaloniaIncludeApplicationIconAsWindowIcon
Assert-True ($applicationIcon.Replace('\', '/').EndsWith('resources/branding/fovium.ico', [StringComparison]::OrdinalIgnoreCase)) 'Fovium.csproj does not point ApplicationIcon at the owned ICO.'
Assert-True ($includeWindowIcon -eq 'true') 'Avalonia application-icon window bridge is not explicitly enabled.'

$windows = Get-ChildItem (Join-Path $repositoryRoot 'Fovium/Views') -Filter '*.axaml' |
    Where-Object { [IO.File]::ReadAllText($_.FullName).TrimStart().StartsWith('<Window ', [StringComparison]::Ordinal) }
Assert-True ($windows.Count -eq 4) "Expected four top-level Window owners, found $($windows.Count)."
foreach ($window in $windows) {
    $text = [IO.File]::ReadAllText($window.FullName)
    Assert-True (-not [regex]::IsMatch($text, '\sIcon\s*=')) "$($window.Name) overrides the shared application icon."
}

Test-ReadmeLinks

if (-not $SkipBuild) {
    Invoke-Checked @('build', $projectPath, '-c', 'Release', '--nologo')
}

$executables = [Collections.Generic.List[string]]::new()
$buildExecutable = Join-Path $repositoryRoot 'Fovium/bin/Release/net10.0/Fovium.exe'
Assert-True (Test-Path -LiteralPath $buildExecutable -PathType Leaf) "Release build executable is missing: $buildExecutable"
$executables.Add($buildExecutable)

if (-not $SkipPublish) {
    $publishRoot = Join-Path $evidenceRoot 'publish-win-x64'
    Invoke-Checked @('publish', $projectPath, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '--nologo', '-o', $publishRoot)
    $publishExecutable = Join-Path $publishRoot 'Fovium.exe'
    Assert-True (Test-Path -LiteralPath $publishExecutable -PathType Leaf) "Published executable is missing: $publishExecutable"
    $executables.Add($publishExecutable)
}

if ($IsWindows) {
    Add-Type -AssemblyName System.Drawing
    Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class FoviumNativeIcon
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint ExtractIconEx(
        string fileName,
        int iconIndex,
        IntPtr[] largeIcons,
        IntPtr[] smallIcons,
        uint iconCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr icon);
}
'@

    function Get-IconPixelHash {
        param([Drawing.Icon] $Icon)

        $bitmap = $Icon.ToBitmap()
        try {
            $bytes = [byte[]]::new($bitmap.Width * $bitmap.Height * 4)
            $offset = 0
            for ($y = 0; $y -lt $bitmap.Height; $y++) {
                for ($x = 0; $x -lt $bitmap.Width; $x++) {
                    $pixel = $bitmap.GetPixel($x, $y)
                    $bytes[$offset++] = $pixel.R
                    $bytes[$offset++] = $pixel.G
                    $bytes[$offset++] = $pixel.B
                    $bytes[$offset++] = $pixel.A
                }
            }
            return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $sourceIcon = [Drawing.Icon]::new($iconPath, 32, 32)
    try {
        $sourceHash = Get-IconPixelHash $sourceIcon
        foreach ($executable in $executables) {
            $large = [IntPtr[]]::new(1)
            $small = [IntPtr[]]::new(1)
            $count = [FoviumNativeIcon]::ExtractIconEx($executable, 0, $large, $small, 1)
            Assert-True ($count -ge 1 -and $large[0] -ne [IntPtr]::Zero) "No executable icon resource was extracted from $executable."
            try {
                $borrowed = [Drawing.Icon]::FromHandle($large[0])
                $extracted = [Drawing.Icon]$borrowed.Clone()
                try {
                    $actualHash = Get-IconPixelHash $extracted
                    Assert-True ($actualHash -eq $sourceHash) "Executable icon differs from the Fovium source icon: $executable"
                    $bitmap = $extracted.ToBitmap()
                    try {
                        $name = if ($executable.Contains('publish-win-x64')) { 'published-exe-icon.png' } else { 'build-exe-icon.png' }
                        $bitmap.Save((Join-Path $evidenceRoot $name), [Drawing.Imaging.ImageFormat]::Png)
                    }
                    finally {
                        $bitmap.Dispose()
                    }
                }
                finally {
                    $extracted.Dispose()
                }
            }
            finally {
                if ($large[0] -ne [IntPtr]::Zero) { $null = [FoviumNativeIcon]::DestroyIcon($large[0]) }
                if ($small[0] -ne [IntPtr]::Zero) { $null = [FoviumNativeIcon]::DestroyIcon($small[0]) }
            }
        }
    }
    finally {
        $sourceIcon.Dispose()
    }
}

Write-Host "Branding verification passed. ICO frames: $($frames.Width -join ', ') px."
Write-Host "Verified executables: $($executables.Count)."
