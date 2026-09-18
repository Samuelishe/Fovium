[CmdletBinding()]
param(
    [string]$CacheDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\color-taxonomy-audit\references')
)

$ErrorActionPreference = 'Stop'
$resolvedCache = [System.IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Force -Path $resolvedCache | Out-Null

$sources = @(
    @{
        Id = 'xkcd'
        File = 'xkcd-rgb.txt'
        Url = 'https://xkcd.com/color/rgb.txt'
        Version = 'official color-survey export; retrieved snapshot'
        License = 'No explicit dataset license stated; ignored local audit cache only'
    },
    @{
        Id = 'css'
        File = 'css-color-4.html'
        Url = 'https://www.w3.org/TR/2026/CRD-css-color-4-20260825/'
        Version = 'CSS Color Module Level 4 CR Draft 2026-08-25'
        License = 'W3C Document License; ignored local audit cache only'
    },
    @{
        Id = 'meodai'
        File = 'meodai-colornames.csv'
        Url = 'https://raw.githubusercontent.com/meodai/color-names/cc5fc08de437ea2522d32f751cecb4aa1e96f8e3/src/colornames.csv'
        Version = 'commit cc5fc08de437ea2522d32f751cecb4aa1e96f8e3'
        License = 'MIT; correlated secondary reference, ignored local audit cache only'
    },
    @{
        Id = 'iscc-nbs'
        File = 'nbscircular553.pdf'
        Url = 'https://nvlpubs.nist.gov/nistpubs/Legacy/circ/nbscircular553.pdf'
        Version = 'NBS Circular 553 (1955), NIST-hosted scan'
        License = 'U.S. Government publication; evaluated as methodology, not parsed as sRGB anchors'
    },
    @{
        Id = 'iscc-nbs-centroids'
        File = 'nbs-iscc.txt'
        Url = 'https://raw.githubusercontent.com/taktoa/slib/05160e4ce21c65f99fea78dc4b29463e2c14bb22/nbs-iscc.txt'
        Version = 'taktoa/slib commit 05160e4ce21c65f99fea78dc4b29463e2c14bb22'
        License = 'File-header redistribution permission; ignored local audit cache only'
    }
)

$provenance = foreach ($source in $sources) {
    $destination = Join-Path $resolvedCache $source.File
    Invoke-WebRequest -Uri $source.Url -OutFile $destination -UseBasicParsing
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant()
    [pscustomobject][ordered]@{
        id = $source.Id
        source = $source.Url
        version = $source.Version
        license = $source.License
        retrievedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        file = $source.File
        sha256 = $hash
    }
}

$provenance |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resolvedCache 'provenance.json') -Encoding utf8

Write-Output "Color taxonomy reference cache: $resolvedCache"
$provenance | Format-Table id, version, sha256
