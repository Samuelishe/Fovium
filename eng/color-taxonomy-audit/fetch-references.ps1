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
        Independence = 'Independent'
        IndependenceGroup = 'iscc-nbs'
        CachePolicy = 'IgnoredCacheOnly'
    },
    @{
        Id = 'ridgway-1912'
        File = 'ridgway-1912.txt'
        Url = 'https://archive.org/download/colorstandardsco00ridg/colorstandardsco00ridg_djvu.txt'
        Version = 'Robert Ridgway, Color Standards and Color Nomenclature (1912), Internet Archive OCR snapshot'
        License = 'Public domain; Smithsonian Libraries identifies the work as CC0/public domain'
        Independence = 'Independent'
        IndependenceGroup = 'ridgway'
        CachePolicy = 'IgnoredCacheOnly'
    },
    @{
        Id = 'werner-1821'
        File = 'werner-1821.txt'
        Url = 'https://archive.org/download/wernersnomencla00wern/wernersnomencla00wern_djvu.txt'
        Version = "Werner/Syme, Werner's Nomenclature of Colours (1821), Internet Archive OCR snapshot"
        License = 'Public domain; Smithsonian Libraries identifies the work as CC0/public domain'
        Independence = 'Independent'
        IndependenceGroup = 'werner'
        CachePolicy = 'IgnoredCacheOnly'
    }
)

foreach ($source in $sources) {
    if (-not $source.ContainsKey('Independence')) {
        $source.Independence = switch ($source.Id) {
            'meodai' { 'Correlated' }
            'css' { 'Independent' }
            'xkcd' { 'Independent' }
            default { 'Independent' }
        }
        $source.IndependenceGroup = switch ($source.Id) {
            'meodai' { 'meodai-color-names' }
            'iscc-nbs' { 'iscc-nbs' }
            default { $source.Id }
        }
        $source.CachePolicy = 'IgnoredCacheOnly'
    }
}

$wikidataQuery = @'
SELECT ?item ?itemLabel ?hex WHERE {
  ?item wdt:P31/wdt:P279* wd:Q1075;
        wdt:P465 ?hex.
  FILTER(REGEX(STR(?hex), "^[0-9A-Fa-f]{6}$"))
  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
}
ORDER BY ?itemLabel ?hex
'@
$wikidataDestination = Join-Path $resolvedCache 'wikidata-colors.csv'
Invoke-WebRequest `
    -Uri 'https://query.wikidata.org/sparql' `
    -Method Post `
    -Body @{ query = $wikidataQuery } `
    -Headers @{ Accept = 'text/csv'; 'User-Agent' = 'FoviumColorTaxonomyAudit/0.1' } `
    -OutFile $wikidataDestination `
    -UseBasicParsing

$wiktionaryDestination = Join-Path $resolvedCache 'wiktionary-colors.html'
Invoke-WebRequest `
    -Uri 'https://en.wiktionary.org/wiki/Appendix:Colors' `
    -Headers @{ 'User-Agent' = 'FoviumColorTaxonomyAudit/0.1 (research cache; contact repository owner)' } `
    -OutFile $wiktionaryDestination `
    -UseBasicParsing

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
        independence = $source.Independence
        independenceGroup = $source.IndependenceGroup
        cachePolicy = $source.CachePolicy
    }
}

$provenance += [pscustomobject][ordered]@{
    id = 'wikidata-colors'
    source = 'https://query.wikidata.org/sparql; items that are colors and carry sRGB color hex triplet (P465)'
    version = 'live CC0 query snapshot'
    license = 'Wikidata structured data: CC0 1.0'
    retrievedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    file = 'wikidata-colors.csv'
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $wikidataDestination).Hash.ToLowerInvariant()
    independence = 'Uncertain'
    independenceGroup = 'wikimedia-structured'
    cachePolicy = 'IgnoredCacheOnly'
}

$provenance += [pscustomobject][ordered]@{
    id = 'wiktionary-colors'
    source = 'https://en.wiktionary.org/wiki/Appendix:Colors'
    version = 'live English color-name appendix snapshot'
    license = 'Wiktionary text: CC BY-SA 4.0 / GFDL; names used as lexical evidence in ignored cache only'
    retrievedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    file = 'wiktionary-colors.html'
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $wiktionaryDestination).Hash.ToLowerInvariant()
    independence = 'Independent'
    independenceGroup = 'wiktionary'
    cachePolicy = 'IgnoredCacheOnly'
}

$provenance |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resolvedCache 'provenance.json') -Encoding utf8

Write-Output "Color taxonomy reference cache: $resolvedCache"
$provenance | Format-Table id, version, sha256
