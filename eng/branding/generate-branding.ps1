[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [string] $EvidenceDirectory
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'resources/branding'
}
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repositoryRoot 'artifacts/branding'
}

Add-Type -AssemblyName System.Drawing

$trackedOutput = [IO.Path]::GetFullPath($OutputDirectory)
$evidenceOutput = [IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($trackedOutput) | Out-Null
[IO.Directory]::CreateDirectory($evidenceOutput) | Out-Null

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

function Add-RoundedFill {
    param(
        [Drawing.Graphics] $Graphics,
        [Drawing.Brush] $Brush,
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = New-RoundedRectanglePath $X $Y $Width $Height $Radius
    try {
        $Graphics.FillPath($Brush, $path)
    }
    finally {
        $path.Dispose()
    }
}

function New-OffsetFieldPath {
    param(
        [ValidateSet('UpperLeft', 'LowerRight')]
        [string] $Corner,
        [float] $Scale,
        [switch] $Small
    )

    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    if ($Small) {
        $points = [Drawing.PointF[]]@(
            [Drawing.PointF]::new(45 * $Scale, 45 * $Scale),
            [Drawing.PointF]::new(176 * $Scale, 45 * $Scale),
            [Drawing.PointF]::new(176 * $Scale, 84 * $Scale),
            [Drawing.PointF]::new(84 * $Scale, 84 * $Scale),
            [Drawing.PointF]::new(84 * $Scale, 176 * $Scale),
            [Drawing.PointF]::new(45 * $Scale, 176 * $Scale)
        )
        $path.AddPolygon($points)
    }
    else {
        $path.StartFigure()
        $path.AddLine(60 * $Scale, 46 * $Scale, 162 * $Scale, 46 * $Scale)
        $path.AddBezier(162 * $Scale, 46 * $Scale, 169 * $Scale, 46 * $Scale, 174 * $Scale, 51 * $Scale, 174 * $Scale, 58 * $Scale)
        $path.AddLine(174 * $Scale, 58 * $Scale, 174 * $Scale, 70 * $Scale)
        $path.AddBezier(174 * $Scale, 70 * $Scale, 174 * $Scale, 77 * $Scale, 169 * $Scale, 82 * $Scale, 162 * $Scale, 82 * $Scale)
        $path.AddLine(162 * $Scale, 82 * $Scale, 82 * $Scale, 82 * $Scale)
        $path.AddLine(82 * $Scale, 82 * $Scale, 82 * $Scale, 162 * $Scale)
        $path.AddBezier(82 * $Scale, 162 * $Scale, 82 * $Scale, 169 * $Scale, 77 * $Scale, 174 * $Scale, 70 * $Scale, 174 * $Scale)
        $path.AddLine(70 * $Scale, 174 * $Scale, 58 * $Scale, 174 * $Scale)
        $path.AddBezier(58 * $Scale, 174 * $Scale, 51 * $Scale, 174 * $Scale, 46 * $Scale, 169 * $Scale, 46 * $Scale, 162 * $Scale)
        $path.AddLine(46 * $Scale, 162 * $Scale, 46 * $Scale, 60 * $Scale)
        $path.AddBezier(46 * $Scale, 60 * $Scale, 46 * $Scale, 52 * $Scale, 52 * $Scale, 46 * $Scale, 60 * $Scale, 46 * $Scale)
        $path.CloseFigure()
    }

    if ($Corner -eq 'LowerRight') {
        $mirror = [Drawing.Drawing2D.Matrix]::new()
        try {
            # The second corner is the exact 180-degree counterpart of the first,
            # offset evenly on both axes to create two identical optical joints.
            $mirror.RotateAt(180, [Drawing.PointF]::new(130 * $Scale, 130 * $Scale))
            $path.Transform($mirror)
        }
        finally {
            $mirror.Dispose()
        }
    }
    return $path
}

function New-IconBitmap {
    param(
        [ValidateSet('OffsetField', 'FieldGate', 'LightSlit')]
        [string] $Concept,
        [int] $Size
    )

    $scale = 4
    $canvas = $Size * $scale
    $bitmap = [Drawing.Bitmap]::new($canvas, $canvas, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([Drawing.Color]::Transparent)
        $k = $canvas / 256.0

        $backgroundPath = New-RoundedRectanglePath (8 * $k) (8 * $k) (240 * $k) (240 * $k) (54 * $k)
        $background = if ($Size -le 24) {
            [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#11141B'))
        }
        else {
            [Drawing.Drawing2D.LinearGradientBrush]::new(
                [Drawing.RectangleF]::new(8 * $k, 8 * $k, 240 * $k, 240 * $k),
                [Drawing.ColorTranslator]::FromHtml('#1B1E29'),
                [Drawing.ColorTranslator]::FromHtml('#090B10'),
                55.0)
        }
        try {
            $graphics.FillPath($background, $backgroundPath)
        }
        finally {
            $background.Dispose()
            $backgroundPath.Dispose()
        }

        $ivory = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F4F1E8'))
        $violet = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#7377F2'))
        $warm = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#EFA76B'))
        try {
            switch ($Concept) {
                'OffsetField' {
                    $isSmall = $Size -le 24
                    $upperLeft = New-OffsetFieldPath -Corner UpperLeft -Scale $k -Small:$isSmall
                    $lowerRight = New-OffsetFieldPath -Corner LowerRight -Scale $k -Small:$isSmall
                    $ivoryMark = if ($isSmall) {
                        [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FFF8E9'))
                    }
                    else {
                        [Drawing.Drawing2D.LinearGradientBrush]::new(
                            [Drawing.RectangleF]::new(46 * $k, 46 * $k, 128 * $k, 128 * $k),
                            [Drawing.ColorTranslator]::FromHtml('#FFF9EB'),
                            [Drawing.ColorTranslator]::FromHtml('#DED9D1'),
                            50.0)
                    }
                    $violetMark = if ($isSmall) {
                        [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#8589FF'))
                    }
                    else {
                        [Drawing.Drawing2D.LinearGradientBrush]::new(
                            [Drawing.RectangleF]::new(76 * $k, 76 * $k, 134 * $k, 134 * $k),
                            [Drawing.ColorTranslator]::FromHtml('#9698FF'),
                            [Drawing.ColorTranslator]::FromHtml('#5A5FDC'),
                            45.0)
                    }
                    try {
                        $graphics.FillPath($ivoryMark, $upperLeft)
                        $graphics.FillPath($violetMark, $lowerRight)
                    }
                    finally {
                        $violetMark.Dispose()
                        $ivoryMark.Dispose()
                        $lowerRight.Dispose()
                        $upperLeft.Dispose()
                    }
                }
                'FieldGate' {
                    $bar = if ($Size -le 24) { 31 } else { 27 }
                    Add-RoundedFill $graphics $ivory (50 * $k) (48 * $k) ($bar * $k) (160 * $k) (9 * $k)
                    Add-RoundedFill $graphics $ivory (50 * $k) (48 * $k) (153 * $k) ($bar * $k) (9 * $k)
                    Add-RoundedFill $graphics $ivory (50 * $k) (181 * $k) (116 * $k) ($bar * $k) (9 * $k)
                    Add-RoundedFill $graphics $warm (179 * $k) (94 * $k) (27 * $k) (75 * $k) (10 * $k)
                }
                'LightSlit' {
                    Add-RoundedFill $graphics $ivory (66 * $k) (43 * $k) (96 * $k) (170 * $k) (25 * $k)
                    Add-RoundedFill $graphics $violet (135 * $k) (66 * $k) (48 * $k) (148 * $k) (18 * $k)
                    $cut = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#111318'))
                    try {
                        Add-RoundedFill $graphics $cut (91 * $k) (75 * $k) (50 * $k) (107 * $k) (14 * $k)
                    }
                    finally {
                        $cut.Dispose()
                    }
                }
            }
        }
        finally {
            $ivory.Dispose()
            $violet.Dispose()
            $warm.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    $result = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $result.SetResolution(96, 96)
    $downsample = [Drawing.Graphics]::FromImage($result)
    try {
        $downsample.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $downsample.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $downsample.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $downsample.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $downsample.DrawImage($bitmap, 0, 0, $Size, $Size)
    }
    finally {
        $downsample.Dispose()
        $bitmap.Dispose()
    }
    return $result
}

function ConvertTo-PngBytes {
    param([Drawing.Bitmap] $Bitmap)

    $stream = [IO.MemoryStream]::new()
    try {
        $Bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

function Write-MultiResolutionIcon {
    param(
        [string] $Path,
        [int[]] $Sizes
    )

    $frames = foreach ($size in $Sizes) {
        $bitmap = New-IconBitmap -Concept OffsetField -Size $size
        try {
            [pscustomobject]@{ Size = $size; Bytes = ConvertTo-PngBytes $bitmap }
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $stream = [IO.File]::Create($Path)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)
        $offset = 6 + (16 * $frames.Count)
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) {
            $writer.Write([byte[]]$frame.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Write-ContactSheet {
    param([string] $Path)

    $sheet = [Drawing.Bitmap]::new(1500, 1080, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($sheet)
    $titleFont = [Drawing.Font]::new('Segoe UI', 23, [Drawing.FontStyle]::Bold)
    $labelFont = [Drawing.Font]::new('Segoe UI', 12, [Drawing.FontStyle]::Regular)
    $smallFont = [Drawing.Font]::new('Segoe UI', 10, [Drawing.FontStyle]::Regular)
    $dark = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#17191F'))
    $light = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F2F3F5'))
    $taskbar = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#252830'))
    $ink = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#17191F'))
    $paperInk = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F4F1E8'))
    try {
        $graphics.Clear([Drawing.Color]::White)
        $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        $graphics.DrawString('Fovium icon concepts — light, dark, and taskbar-scale evidence', $titleFont, $ink, 42, 28)
        $concepts = @(
            @{ Id = 'FieldGate'; Name = '01  Field gate' },
            @{ Id = 'OffsetField'; Name = '02  Offset field (selected)' },
            @{ Id = 'LightSlit'; Name = '03  Light slit' }
        )
        $sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
        for ($row = 0; $row -lt $concepts.Count; $row++) {
            $top = 112 + ($row * 316)
            $graphics.FillRectangle($light, 34, $top, 920, 288)
            $graphics.FillRectangle($dark, 954, $top, 510, 288)
            $graphics.DrawString($concepts[$row].Name, $labelFont, $ink, 50, $top + 12)
            $x = 52
            foreach ($size in $sizes) {
                $bitmap = New-IconBitmap -Concept $concepts[$row].Id -Size $size
                try {
                    $graphics.DrawImageUnscaled($bitmap, $x, $top + 42 + [int]((256 - $size) / 2))
                    $graphics.DrawString("$size", $smallFont, $ink, $x, $top + 262)
                }
                finally {
                    $bitmap.Dispose()
                }
                $x += [Math]::Max($size + 26, 70)
            }
            $graphics.FillRectangle($taskbar, 1000, $top + 106, 420, 68)
            $taskX = 1026
            foreach ($size in @(16, 20, 24, 32, 48)) {
                $bitmap = New-IconBitmap -Concept $concepts[$row].Id -Size $size
                try {
                    $graphics.DrawImageUnscaled($bitmap, $taskX, $top + 116 + [int]((48 - $size) / 2))
                }
                finally {
                    $bitmap.Dispose()
                }
                $taskX += 72
            }
            $graphics.DrawString('Windows-like surface', $smallFont, $paperInk, 1000, $top + 180)
        }
        $sheet.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $paperInk.Dispose()
        $ink.Dispose()
        $taskbar.Dispose()
        $light.Dispose()
        $dark.Dispose()
        $smallFont.Dispose()
        $labelFont.Dispose()
        $titleFont.Dispose()
        $graphics.Dispose()
        $sheet.Dispose()
    }
}

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" role="img" aria-labelledby="title desc">
  <title id="title">Fovium application icon</title>
  <desc id="desc">Two precise offset frame corners define an open photographic field.</desc>
  <defs>
    <linearGradient id="surface" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#1B1E29"/>
      <stop offset="1" stop-color="#090B10"/>
    </linearGradient>
    <linearGradient id="light" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#FFF9EB"/>
      <stop offset="1" stop-color="#DED9D1"/>
    </linearGradient>
    <linearGradient id="violet" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#9698FF"/>
      <stop offset="1" stop-color="#5A5FDC"/>
    </linearGradient>
    <path id="corner" d="M60 46H162C169 46 174 51 174 58V70C174 77 169 82 162 82H82V162C82 169 77 174 70 174H58C51 174 46 169 46 162V60C46 52 52 46 60 46Z"/>
  </defs>
  <rect x="8" y="8" width="240" height="240" rx="54" fill="url(#surface)"/>
  <use href="#corner" fill="url(#light)"/>
  <use href="#corner" fill="url(#violet)" transform="rotate(180 130 130)"/>
</svg>
'@
[IO.File]::WriteAllText((Join-Path $trackedOutput 'fovium-app-icon.svg'), $svg, [Text.UTF8Encoding]::new($false))

foreach ($size in @(64, 128, 256)) {
    $bitmap = New-IconBitmap -Concept OffsetField -Size $size
    try {
        $bitmap.Save((Join-Path $trackedOutput "fovium-app-icon-$size.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

$iconSizes = @(16, 20, 24, 32, 48, 64, 128, 256)
Write-MultiResolutionIcon -Path (Join-Path $trackedOutput 'fovium.ico') -Sizes $iconSizes
Write-ContactSheet -Path (Join-Path $evidenceOutput 'icon-concepts.png')

$selectedSheet = [Drawing.Bitmap]::new(1800, 320, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
$selectedGraphics = [Drawing.Graphics]::FromImage($selectedSheet)
$selectedDark = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#17191F'))
$selectedLight = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F2F3F5'))
try {
    $selectedGraphics.FillRectangle($selectedLight, 0, 0, 900, 320)
    $selectedGraphics.FillRectangle($selectedDark, 900, 0, 900, 320)
    $x = 32
    foreach ($size in $iconSizes) {
        $bitmap = New-IconBitmap -Concept OffsetField -Size $size
        try {
            $selectedGraphics.DrawImageUnscaled($bitmap, $x, 32 + [int]((256 - $size) / 2))
            $selectedGraphics.DrawImageUnscaled($bitmap, 900 + $x, 32 + [int]((256 - $size) / 2))
        }
        finally {
            $bitmap.Dispose()
        }
        $x += [Math]::Max($size + 20, 54)
    }
    $selectedSheet.Save((Join-Path $evidenceOutput 'selected-small-sizes.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $selectedLight.Dispose()
    $selectedDark.Dispose()
    $selectedGraphics.Dispose()
    $selectedSheet.Dispose()
}

Write-Host "Branding assets: $trackedOutput"
Write-Host "Visual evidence: $evidenceOutput"
