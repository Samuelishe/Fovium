[CmdletBinding()]
param(
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) {
    throw 'The current README capture harness requires a visible Windows desktop.'
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$projectPath = Join-Path $repositoryRoot 'Fovium/Fovium.csproj'
$executablePath = Join-Path $repositoryRoot 'Fovium/bin/Release/net10.0/Fovium.exe'
$evidenceRoot = Join-Path $repositoryRoot 'artifacts/branding'
$captureHostRoot = Join-Path $evidenceRoot 'capture-host'
$captureHostProject = Join-Path $captureHostRoot 'Fovium.BrandingCapture.csproj'
$captureHostExecutable = Join-Path $captureHostRoot 'bin/Release/net10.0/Fovium.BrandingCapture.exe'
$readmeAssetRoot = Join-Path $repositoryRoot 'docs/assets/readme'
$syntheticPath = Join-Path $evidenceRoot 'fovium-synthetic-study.png'
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
[IO.Directory]::CreateDirectory($readmeAssetRoot) | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class FoviumWindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr(IntPtr window, int index);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr state);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    public static IntPtr[] GetVisibleWindows(int processId)
    {
        var windows = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner == (uint)processId && IsWindowVisible(window))
            {
                windows.Add(window);
            }
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static IntPtr GetWindowIcon(IntPtr window)
    {
        const uint WmGetIcon = 0x007F;
        var icon = SendMessage(window, WmGetIcon, new IntPtr(1), IntPtr.Zero);
        if (icon == IntPtr.Zero) icon = SendMessage(window, WmGetIcon, new IntPtr(2), IntPtr.Zero);
        if (icon == IntPtr.Zero) icon = SendMessage(window, WmGetIcon, IntPtr.Zero, IntPtr.Zero);
        if (icon == IntPtr.Zero) icon = GetClassLongPtr(window, -14);
        if (icon == IntPtr.Zero) icon = GetClassLongPtr(window, -34);
        return icon;
    }
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

function Assert-RuntimeWindowIcon {
    param(
        [IntPtr] $Window,
        [string] $EvidenceName
    )

    $handle = [FoviumWindowCapture]::GetWindowIcon($Window)
    if ($handle -eq [IntPtr]::Zero) {
        throw "$EvidenceName does not expose a native window icon."
    }

    $borrowed = [Drawing.Icon]::FromHandle($handle)
    $runtime = [Drawing.Icon]$borrowed.Clone()
    try {
        $bitmap = $runtime.ToBitmap()
        $source = [Drawing.Icon]::new(
            (Join-Path $repositoryRoot 'resources/branding/fovium.ico'),
            $bitmap.Width,
            $bitmap.Height)
        $sourceBitmap = $source.ToBitmap()
        try {
            $bitmap.Save((Join-Path $evidenceRoot "$EvidenceName-window-icon.png"), [Drawing.Imaging.ImageFormat]::Png)
            $sourceBitmap.Save((Join-Path $evidenceRoot "$EvidenceName-source-icon.png"), [Drawing.Imaging.ImageFormat]::Png)

            if ($bitmap.Width -ne $sourceBitmap.Width -or $bitmap.Height -ne $sourceBitmap.Height) {
                throw "$EvidenceName native icon dimensions differ from the requested source frame."
            }
            [long]$difference = 0
            for ($y = 0; $y -lt $bitmap.Height; $y++) {
                for ($x = 0; $x -lt $bitmap.Width; $x++) {
                    $actual = $bitmap.GetPixel($x, $y)
                    $expected = $sourceBitmap.GetPixel($x, $y)
                    $difference += [Math]::Abs([int]$actual.R - [int]$expected.R)
                    $difference += [Math]::Abs([int]$actual.G - [int]$expected.G)
                    $difference += [Math]::Abs([int]$actual.B - [int]$expected.B)
                    $difference += [Math]::Abs([int]$actual.A - [int]$expected.A)
                }
            }
            $meanDifference = $difference / [double]($bitmap.Width * $bitmap.Height * 4)
            if ($meanDifference -gt 18) {
                throw "$EvidenceName native window icon differs materially from the Fovium project icon (mean channel difference $([Math]::Round($meanDifference, 2)))."
            }
            Write-Host "$EvidenceName native window icon: $($bitmap.Width)x$($bitmap.Height), mean channel difference $([Math]::Round($meanDifference, 2))"
        }
        finally {
            $source.Dispose()
            $sourceBitmap.Dispose()
            $bitmap.Dispose()
        }
    }
    finally {
        $runtime.Dispose()
    }
}

function Add-ClosedCurve {
    param(
        [Drawing.Graphics] $Graphics,
        [Drawing.Brush] $Brush,
        [Drawing.PointF[]] $Points,
        [float] $Tension = 0.55
    )

    $Graphics.FillClosedCurve($Brush, $Points, [Drawing.Drawing2D.FillMode]::Winding, $Tension)
}

function New-RoundedRectanglePath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Write-EnglishCaptureHost {
    [IO.Directory]::CreateDirectory($captureHostRoot) | Out-Null
    $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../Fovium/Fovium.csproj" />
  </ItemGroup>
</Project>
'@
    $program = @'
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;

var culture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;

var fovium = Assembly.Load("Fovium");
var entryPoint = fovium.EntryPoint ?? throw new InvalidOperationException("Fovium entry point was not found.");
try
{
    entryPoint.Invoke(null, new object?[] { args });
}
catch (TargetInvocationException exception) when (exception.InnerException is not null)
{
    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
}
'@
    [IO.File]::WriteAllText($captureHostProject, $project, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $captureHostRoot 'Program.cs'), $program, [Text.UTF8Encoding]::new($false))
}

function New-SyntheticStudy {
    param([string] $Path)

    $width = 1920
    $height = 1200
    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $skyRectangle = [Drawing.Rectangle]::new(0, 0, $width, $height)
        $sky = [Drawing.Drawing2D.LinearGradientBrush]::new(
            $skyRectangle,
            [Drawing.ColorTranslator]::FromHtml('#171A2C'),
            [Drawing.ColorTranslator]::FromHtml('#E1A993'),
            90.0)
        try {
            $blend = [Drawing.Drawing2D.ColorBlend]::new(6)
            $blend.Colors = @(
                [Drawing.ColorTranslator]::FromHtml('#111523'),
                [Drawing.ColorTranslator]::FromHtml('#262C48'),
                [Drawing.ColorTranslator]::FromHtml('#5E5877'),
                [Drawing.ColorTranslator]::FromHtml('#A87582'),
                [Drawing.ColorTranslator]::FromHtml('#D99A87'),
                [Drawing.ColorTranslator]::FromHtml('#E9B49B')
            )
            $blend.Positions = [single[]](0.0, 0.25, 0.49, 0.69, 0.86, 1.0)
            $sky.InterpolationColors = $blend
            $graphics.FillRectangle($sky, $skyRectangle)
        }
        finally {
            $sky.Dispose()
        }

        $sun = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F4D9B3'))
        try { $graphics.FillEllipse($sun, 1430, 206, 236, 236) } finally { $sun.Dispose() }

        $ridgeBack = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#796374'))
        $ridgeMid = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#4B435B'))
        $ridgeFront = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#292D43'))
        $foreground = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#131827'))
        try {
            Add-ClosedCurve $graphics $ridgeBack ([Drawing.PointF[]]@(
                [Drawing.PointF]::new(-100, 770), [Drawing.PointF]::new(300, 630),
                [Drawing.PointF]::new(690, 706), [Drawing.PointF]::new(1080, 560),
                [Drawing.PointF]::new(1510, 700), [Drawing.PointF]::new(2020, 610),
                [Drawing.PointF]::new(2020, 1300), [Drawing.PointF]::new(-100, 1300))) 0.48
            Add-ClosedCurve $graphics $ridgeMid ([Drawing.PointF[]]@(
                [Drawing.PointF]::new(-100, 875), [Drawing.PointF]::new(350, 735),
                [Drawing.PointF]::new(780, 824), [Drawing.PointF]::new(1250, 664),
                [Drawing.PointF]::new(2020, 850), [Drawing.PointF]::new(2020, 1300),
                [Drawing.PointF]::new(-100, 1300))) 0.4
            Add-ClosedCurve $graphics $ridgeFront ([Drawing.PointF[]]@(
                [Drawing.PointF]::new(-100, 980), [Drawing.PointF]::new(420, 815),
                [Drawing.PointF]::new(890, 970), [Drawing.PointF]::new(1410, 790),
                [Drawing.PointF]::new(2020, 950), [Drawing.PointF]::new(2020, 1300),
                [Drawing.PointF]::new(-100, 1300))) 0.32
            Add-ClosedCurve $graphics $foreground ([Drawing.PointF[]]@(
                [Drawing.PointF]::new(-100, 1085), [Drawing.PointF]::new(470, 930),
                [Drawing.PointF]::new(980, 1090), [Drawing.PointF]::new(1530, 930),
                [Drawing.PointF]::new(2020, 1060), [Drawing.PointF]::new(2020, 1300),
                [Drawing.PointF]::new(-100, 1300))) 0.26
        }
        finally {
            $foreground.Dispose()
            $ridgeFront.Dispose()
            $ridgeMid.Dispose()
            $ridgeBack.Dispose()
        }

        $architecture = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#DCC4AD'))
        $architectureSide = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#9E7E7A'))
        $shadow = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#20263A'))
        $light = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F0D5B6'))
        $violet = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#7377F2'))
        try {
            $graphics.FillPolygon($shadow, [Drawing.Point[]]@(
                [Drawing.Point]::new(940, 938), [Drawing.Point]::new(1510, 938),
                [Drawing.Point]::new(1720, 1055), [Drawing.Point]::new(1120, 1068)))
            $graphics.FillPolygon($architecture, [Drawing.Point[]]@(
                [Drawing.Point]::new(1040, 630), [Drawing.Point]::new(1435, 554),
                [Drawing.Point]::new(1435, 934), [Drawing.Point]::new(1040, 986)))
            $graphics.FillPolygon($architectureSide, [Drawing.Point[]]@(
                [Drawing.Point]::new(1435, 554), [Drawing.Point]::new(1600, 650),
                [Drawing.Point]::new(1600, 910), [Drawing.Point]::new(1435, 934)))
            $graphics.FillPolygon($light, [Drawing.Point[]]@(
                [Drawing.Point]::new(1040, 630), [Drawing.Point]::new(1435, 554),
                [Drawing.Point]::new(1600, 650), [Drawing.Point]::new(1205, 724)))
            $graphics.FillPolygon($violet, [Drawing.Point[]]@(
                [Drawing.Point]::new(1250, 710), [Drawing.Point]::new(1375, 690),
                [Drawing.Point]::new(1375, 943), [Drawing.Point]::new(1250, 960)))
            $graphics.FillPolygon($shadow, [Drawing.Point[]]@(
                [Drawing.Point]::new(1268, 728), [Drawing.Point]::new(1358, 714),
                [Drawing.Point]::new(1358, 945), [Drawing.Point]::new(1268, 957)))
        }
        finally {
            $violet.Dispose()
            $light.Dispose()
            $shadow.Dispose()
            $architectureSide.Dispose()
            $architecture.Dispose()
        }

        $haze = [Drawing.Pen]::new([Drawing.Color]::FromArgb(30, 255, 238, 219), 2)
        try {
            $graphics.DrawLine($haze, 120, 626, 845, 626)
            $graphics.DrawLine($haze, 245, 642, 720, 642)
        }
        finally {
            $haze.Dispose()
        }

        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Save-WindowCapture {
    param(
        [IntPtr] $Window,
        [string] $Path
    )

    $rectangle = [FoviumWindowCapture+Rect]::new()
    if (-not [FoviumWindowCapture]::GetWindowRect($Window, [ref]$rectangle)) {
        throw 'Could not read Fovium window bounds.'
    }
    $width = $rectangle.Right - $rectangle.Left
    $height = $rectangle.Bottom - $rectangle.Top
    if ($width -lt 640 -or $height -lt 480) {
        throw "Fovium capture bounds are unexpectedly small: ${width}x${height}."
    }

    $bitmap = [Drawing.Bitmap]::new($width, $height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rectangle.Left, $rectangle.Top, 0, 0, [Drawing.Size]::new($width, $height))
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Write-HeroComposition {
    param(
        [string] $CapturePath,
        [string] $OutputPath
    )

    $capture = [Drawing.Image]::FromFile($CapturePath)
    $hero = [Drawing.Bitmap]::new(1800, 1080, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($hero)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $surface = [Drawing.Drawing2D.LinearGradientBrush]::new(
            [Drawing.Rectangle]::new(0, 0, $hero.Width, $hero.Height),
            [Drawing.ColorTranslator]::FromHtml('#181A25'),
            [Drawing.ColorTranslator]::FromHtml('#29243D'),
            35.0)
        try { $graphics.FillRectangle($surface, 0, 0, $hero.Width, $hero.Height) } finally { $surface.Dispose() }

        $ambient = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(22, 132, 137, 255))
        try { $graphics.FillEllipse($ambient, 1180, -360, 920, 920) } finally { $ambient.Dispose() }

        $targetWidth = 1660
        $targetHeight = [int][Math]::Round($capture.Height * ($targetWidth / [double]$capture.Width))
        if ($targetHeight -gt 936) {
            $targetHeight = 936
            $targetWidth = [int][Math]::Round($capture.Width * ($targetHeight / [double]$capture.Height))
        }
        $x = [int](($hero.Width - $targetWidth) / 2)
        $y = [int](($hero.Height - $targetHeight) / 2)

        foreach ($shadowSpec in @(
            @{ Inflate = 34; Alpha = 18 },
            @{ Inflate = 22; Alpha = 28 },
            @{ Inflate = 12; Alpha = 44 }
        )) {
            $inflate = [int]$shadowSpec.Inflate
            $shadowPath = New-RoundedRectanglePath ($x - $inflate) ($y - $inflate + 12) ($targetWidth + (2 * $inflate)) ($targetHeight + (2 * $inflate)) (30 + $inflate)
            $shadow = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int]$shadowSpec.Alpha, 0, 0, 0))
            try { $graphics.FillPath($shadow, $shadowPath) } finally { $shadow.Dispose(); $shadowPath.Dispose() }
        }

        $windowPath = New-RoundedRectanglePath $x $y $targetWidth $targetHeight 22
        try {
            $graphics.SetClip($windowPath)
            $graphics.DrawImage($capture, $x, $y, $targetWidth, $targetHeight)
            $graphics.ResetClip()
            $border = [Drawing.Pen]::new([Drawing.Color]::FromArgb(70, 255, 255, 255), 1)
            try { $graphics.DrawPath($border, $windowPath) } finally { $border.Dispose() }
        }
        finally {
            $windowPath.Dispose()
        }

        $codec = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
        $quality = [Drawing.Imaging.EncoderParameters]::new(1)
        $quality.Param[0] = [Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality, [long]91)
        try { $hero.Save($OutputPath, $codec, $quality) } finally { $quality.Dispose() }
    }
    finally {
        $graphics.Dispose()
        $hero.Dispose()
        $capture.Dispose()
    }
}

New-SyntheticStudy $syntheticPath
Write-EnglishCaptureHost

if (-not $SkipBuild) {
    & dotnet build $projectPath -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release Fovium build failed.' }
}
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Fovium executable is missing: $executablePath"
}

& dotnet build $captureHostProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'English branding capture host build failed.' }
if (-not (Test-Path -LiteralPath $captureHostExecutable -PathType Leaf)) {
    throw "Branding capture host is missing: $captureHostExecutable"
}

$process = Start-Process -FilePath $captureHostExecutable -ArgumentList @($syntheticPath) -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Fovium did not create a main window before the capture deadline.'
    }

    Start-Sleep -Seconds 3
    $null = [FoviumWindowCapture]::SetForegroundWindow($process.MainWindowHandle)
    Start-Sleep -Milliseconds 400
    $zeroUiCapture = Join-Path $evidenceRoot 'hero-candidate-zero-ui.png'
    Save-WindowCapture $process.MainWindowHandle $zeroUiCapture

    [Windows.Forms.SendKeys]::SendWait('i')
    Start-Sleep -Seconds 2
    $photoInfoCapture = Join-Path $evidenceRoot 'hero-candidate-photo-info.png'
    Save-WindowCapture $process.MainWindowHandle $photoInfoCapture

    Write-HeroComposition $zeroUiCapture (Join-Path $evidenceRoot 'hero-composition-zero-ui.jpg')
    Write-HeroComposition $photoInfoCapture (Join-Path $evidenceRoot 'hero-composition-photo-info.jpg')
    Write-HeroComposition $photoInfoCapture (Join-Path $readmeAssetRoot 'fovium-hero.jpg')

    [Windows.Forms.SendKeys]::SendWait('i')
    [Windows.Forms.SendKeys]::SendWait('^,')
    Start-Sleep -Seconds 2
    $settingsWindow = [FoviumWindowCapture]::GetVisibleWindows($process.Id) |
        Where-Object { $_ -ne $process.MainWindowHandle } |
        Select-Object -First 1
    if ($settingsWindow -eq [IntPtr]::Zero) {
        throw 'Settings window did not become visible for icon evidence.'
    }
    Save-WindowCapture $settingsWindow (Join-Path $evidenceRoot 'settings-window-english.png')
}
finally {
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(5000)) {
            $process.Kill($true)
            $process.WaitForExit()
        }
    }
    $process.Dispose()
}

# The culture-controlled capture host proves the public artwork in English. Run the
# real apphost separately so window identity evidence remains tied to Fovium.exe.
$iconProcess = Start-Process -FilePath $executablePath -ArgumentList @($syntheticPath) -PassThru
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $iconProcess.Refresh()
    } while ($iconProcess.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
    if ($iconProcess.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'Fovium.exe did not create a main window for icon evidence.'
    }

    Start-Sleep -Seconds 2
    Assert-RuntimeWindowIcon $iconProcess.MainWindowHandle 'viewer'
    $null = [FoviumWindowCapture]::SetForegroundWindow($iconProcess.MainWindowHandle)
    [Windows.Forms.SendKeys]::SendWait('^,')
    Start-Sleep -Seconds 2
    $settingsWindow = [FoviumWindowCapture]::GetVisibleWindows($iconProcess.Id) |
        Where-Object { $_ -ne $iconProcess.MainWindowHandle } |
        Select-Object -First 1
    if ($settingsWindow -eq [IntPtr]::Zero) {
        throw 'Settings window did not become visible for Fovium.exe icon evidence.'
    }
    Assert-RuntimeWindowIcon $settingsWindow 'settings'
    Save-WindowCapture $settingsWindow (Join-Path $evidenceRoot 'settings-window-runtime.png')
}
finally {
    if (-not $iconProcess.HasExited) {
        $null = $iconProcess.CloseMainWindow()
        if (-not $iconProcess.WaitForExit(5000)) {
            $iconProcess.Kill($true)
            $iconProcess.WaitForExit()
        }
    }
    $iconProcess.Dispose()
}

Write-Host "Synthetic source: $syntheticPath"
Write-Host "README hero: $(Join-Path $readmeAssetRoot 'fovium-hero.jpg')"
