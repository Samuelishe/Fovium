param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePath,

    [string] $OutputPath = "resources/color-names/fovium-color-names.json"
)

$ErrorActionPreference = "Stop"

$expectedSourceSha256 = "5dd5a199d58beb3b2121b79a02236ecf88397c3f6fbaff73eb5bc776d435bc60"
$expectedSourceCount = 31915
$targetCount = 1800
$maximumShortNameLength = 12
$requiredBasicNames = @(
    "Black",
    "White",
    "Red",
    "Green",
    "Blue",
    "Yellow",
    "Orange",
    "Purple",
    "Pink",
    "Brown",
    "Grey",
    "Cyan",
    "Magenta",
    "Teal",
    "Navy Blue",
    "Olive",
    "Beige"
)

# The upstream list already rejects offensive and protected-brand submissions.
# This additional bounded filter removes legacy identity/brand descriptors and
# deliberately novelty-oriented vocabulary from Fovium's photographic subset.
$blockedWords = @(
    "Aboriginal", "African", "American", "Arabian", "Asian", "British", "Canadian",
    "Caucasian", "Chinese", "Dutch", "English", "Eskimo", "French", "German", "Greek",
    "Gypsy", "Indian", "Irish", "Italian", "Japanese", "Korean", "Mexican", "Native",
    "Nordic", "Oriental", "Persian", "Russian", "Scottish", "Spanish", "Swedish", "Thai",
    "Tibetan", "Turkish", "Vietnamese", "Welsh", "Skin", "Flesh", "Nude",
    "Barbie", "Barbiecore", "Batman", "Coca", "Disney", "Facebook", "Ferrari", "Google", "Harley",
    "Goldfrapp", "Jedi", "Lego", "Pepsi", "Rolex", "Starbucks", "Tardis", "Tesla", "Tiffany", "Twitter",
    "Alien", "Demon", "Dragon", "Meme", "Monster", "Unicorn", "Vampire",
    "Zombie"
)

function Convert-SrgbChannelToLinear([double] $channel) {
    if ($channel -le 0.04045) {
        return $channel / 12.92
    }

    return [Math]::Pow(($channel + 0.055) / 1.055, 2.4)
}

function Convert-HexToOklab([string] $hex) {
    $red = [Convert]::ToInt32($hex.Substring(1, 2), 16) / 255.0
    $green = [Convert]::ToInt32($hex.Substring(3, 2), 16) / 255.0
    $blue = [Convert]::ToInt32($hex.Substring(5, 2), 16) / 255.0
    [double] $red = Convert-SrgbChannelToLinear $red
    [double] $green = Convert-SrgbChannelToLinear $green
    [double] $blue = Convert-SrgbChannelToLinear $blue

    $l = 0.4122214708 * $red + 0.5363325363 * $green + 0.0514459929 * $blue
    $m = 0.2119034982 * $red + 0.6806995451 * $green + 0.1073969566 * $blue
    $s = 0.0883024619 * $red + 0.2817188376 * $green + 0.6299787005 * $blue
    $lRoot = [Math]::Cbrt($l)
    $mRoot = [Math]::Cbrt($m)
    $sRoot = [Math]::Cbrt($s)

    return @(
        (0.2104542553 * $lRoot + 0.7936177850 * $mRoot - 0.0040720468 * $sRoot)
        (1.9779984951 * $lRoot - 2.4285922050 * $mRoot + 0.4505937099 * $sRoot)
        (0.0259040371 * $lRoot + 0.7827717662 * $mRoot - 0.8086757660 * $sRoot)
    )
}

function Get-DistanceSquared($left, $right) {
    $deltaL = $left.L - $right.L
    $deltaA = $left.A - $right.A
    $deltaB = $left.B - $right.B
    return $deltaL * $deltaL + $deltaA * $deltaA + $deltaB * $deltaB
}

$resolvedSource = (Resolve-Path -LiteralPath $SourcePath).Path
$sourceHash = (Get-FileHash -LiteralPath $resolvedSource -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sourceHash -ne $expectedSourceSha256) {
    throw "Unexpected upstream SHA-256: $sourceHash"
}

$sourceRows = @(Import-Csv -LiteralPath $resolvedSource)
if ($sourceRows.Count -ne $expectedSourceCount) {
    throw "Unexpected upstream row count: $($sourceRows.Count)"
}

$blockedPattern = "(?i)(^|[^\p{L}])(" + (($blockedWords | ForEach-Object { [Regex]::Escape($_) }) -join "|") + ")([^\p{L}]|$)"
$bestOfRows = @($sourceRows | Where-Object { $_.'good name' -eq 'x' })
$shortRows = @($bestOfRows | Where-Object { $_.name.Length -le $maximumShortNameLength })
$eligibleRows = @($sourceRows | Where-Object {
    $_.name -in $requiredBasicNames -or (
        $_.'good name' -eq 'x' -and
        $_.name.Length -le $maximumShortNameLength -and
        $_.name -notmatch '\d' -and
        $_.name -match "^[\p{L}][\p{L}\p{M} '&’.-]*$" -and
        $_.name -notmatch $blockedPattern)
})

$candidates = @($eligibleRows | ForEach-Object {
    $lab = Convert-HexToOklab $_.hex
    [PSCustomObject]@{
        Name = $_.name
        Hex = $_.hex.ToUpperInvariant()
        L = $lab[0]
        A = $lab[1]
        B = $lab[2]
        NearestDistance = [double]::PositiveInfinity
        Selected = $false
    }
} | Sort-Object Name, Hex -Culture en-US -CaseSensitive)

$selected = [System.Collections.Generic.List[object]]::new()
foreach ($requiredName in $requiredBasicNames) {
    $candidate = $candidates | Where-Object { $_.Name -eq $requiredName } | Select-Object -First 1
    if ($null -eq $candidate) {
        throw "Required basic anchor is missing: $requiredName"
    }

    if (!$candidate.Selected) {
        $candidate.Selected = $true
        $selected.Add($candidate)
    }
}

foreach ($candidate in $candidates) {
    if ($candidate.Selected) {
        $candidate.NearestDistance = 0
        continue
    }

    $nearest = [double]::PositiveInfinity
    foreach ($anchor in $selected) {
        $distance = Get-DistanceSquared $candidate $anchor
        if ($distance -lt $nearest) {
            $nearest = $distance
        }
    }

    $candidate.NearestDistance = $nearest
}

while ($selected.Count -lt $targetCount) {
    $next = $candidates |
        Where-Object { !$_.Selected } |
        Sort-Object @{ Expression = 'NearestDistance'; Descending = $true }, Name, Hex -Culture en-US -CaseSensitive |
        Select-Object -First 1
    if ($null -eq $next) {
        throw "Only $($selected.Count) eligible colors remain; target is $targetCount."
    }

    $next.Selected = $true
    $next.NearestDistance = 0
    $selected.Add($next)
    foreach ($candidate in $candidates) {
        if ($candidate.Selected) {
            continue
        }

        $distance = Get-DistanceSquared $candidate $next
        if ($distance -lt $candidate.NearestDistance) {
            $candidate.NearestDistance = $distance
        }
    }
}

$catalog = @($selected |
    Sort-Object Hex -Culture en-US -CaseSensitive |
    ForEach-Object {
        [ordered]@{
            id = "rgb-$($_.Hex.Substring(1).ToLowerInvariant())"
            hex = $_.Hex
            name = $_.Name
        }
    })

$resolvedOutput = [IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
$outputDirectory = [IO.Path]::GetDirectoryName($resolvedOutput)
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$json = $catalog | ConvertTo-Json -Depth 3
[IO.File]::WriteAllText($resolvedOutput, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$outputHash = (Get-FileHash -LiteralPath $resolvedOutput -Algorithm SHA256).Hash.ToLowerInvariant()

[PSCustomObject]@{
    SourceSha256 = $sourceHash
    OriginalCount = $sourceRows.Count
    BestOfCount = $bestOfRows.Count
    ShortCount = $shortRows.Count
    EligibleCount = $eligibleRows.Count
    FinalCount = $catalog.Count
    OutputSha256 = $outputHash
    OutputPath = $resolvedOutput
}
