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
        SourceQuality = 'PrimaryHumanNumeric'
    },
    @{
        Id = 'css'
        File = 'css-color-4.html'
        Url = 'https://www.w3.org/TR/2026/CRD-css-color-4-20260825/'
        Version = 'CSS Color Module Level 4 CR Draft 2026-08-25'
        License = 'W3C Document License; ignored local audit cache only'
        SourceQuality = 'StandardNumeric'
    },
    @{
        Id = 'meodai'
        File = 'meodai-colornames.csv'
        Url = 'https://raw.githubusercontent.com/meodai/color-names/cc5fc08de437ea2522d32f751cecb4aa1e96f8e3/src/colornames.csv'
        Version = 'commit cc5fc08de437ea2522d32f751cecb4aa1e96f8e3'
        License = 'MIT; correlated secondary reference, ignored local audit cache only'
        SourceQuality = 'DerivedNumeric'
    },
    @{
        Id = 'iscc-nbs'
        File = 'nbscircular553.pdf'
        Url = 'https://nvlpubs.nist.gov/nistpubs/Legacy/circ/nbscircular553.pdf'
        Version = 'NBS Circular 553 (1955), NIST-hosted scan'
        License = 'U.S. Government publication; evaluated as methodology, not parsed as sRGB anchors'
        SourceQuality = 'AuthoritativeMethodology'
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
        SourceQuality = 'AuthoritativeDerivedNumeric'
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
        SourceQuality = 'LexicalOnlyHistorical'
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
        SourceQuality = 'LexicalOnlyHistorical'
    },
    @{
        Id = 'uw-labinthewild'
        File = 'uw-color-names.csv'
        Url = 'https://raw.githubusercontent.com/uwdata/color-naming-in-different-languages/f9a0ebedf3de729a755e0454195b13bbb5681909/raw/color_names.csv'
        Version = 'uwdata/color-naming-in-different-languages commit f9a0ebedf3de729a755e0454195b13bbb5681909'
        License = 'No explicit repository license; authors publish the dataset for download; ignored research cache only and redistribution is not asserted'
        Independence = 'Independent'
        IndependenceGroup = 'uw-labinthewild'
        CachePolicy = 'IgnoredCacheOnly'
        SourceQuality = 'PrimaryHumanNumeric'
    },
    @{
        Id = 'stanford-color-reference'
        File = 'stanford-color-reference.csv'
        Url = 'https://raw.githubusercontent.com/futurulus/coop-nets/01b1710b71358b224494d3329cc31b3cff9e10f6/behavioralAnalysis/humanOutput/filteredCorpus.csv'
        Version = 'futurulus/coop-nets commit 01b1710b71358b224494d3329cc31b3cff9e10f6; filtered native-English human corpus'
        License = 'No explicit repository license; public academic corpus; ignored research cache only and redistribution is not asserted'
        Independence = 'Independent'
        IndependenceGroup = 'stanford-color-reference'
        CachePolicy = 'IgnoredCacheOnly'
        SourceQuality = 'PrimaryHumanNumeric'
    },
    @{
        Id = 'iscc-nbs-dictionary'
        File = 'Color-Library-0.021.tar.gz'
        Url = 'https://cpan.metacpan.org/authors/id/R/RO/ROKR/Color-Library-0.021.tar.gz'
        Version = 'Color-Library 0.021 (2011-12-07); NBS/ISCC source dictionaries'
        License = 'Perl 5 license (Artistic 1.0 or GPL-1.0-or-later); dictionary content derives from U.S. Government NBS SP 440'
        Independence = 'Independent'
        IndependenceGroup = 'iscc-nbs'
        CachePolicy = 'IgnoredCacheOnly'
        SourceQuality = 'AuthoritativeDerivedNumeric'
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

    if (-not $source.ContainsKey('SourceQuality')) {
        $source.SourceQuality = 'UncertainProvenance'
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
    if ($source.Id -eq 'iscc-nbs-dictionary') {
        $temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedCache '.nbs-iscc-extract'))
        $dictionaryRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedCache 'nbs-iscc-dictionaries'))
        if (-not $temporaryRoot.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase) -or
            -not $dictionaryRoot.StartsWith($resolvedCache, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Resolved NBS/ISCC extraction paths escaped the reference cache.'
        }

        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
        New-Item -ItemType Directory -Force -Path $temporaryRoot, $dictionaryRoot | Out-Null
        tar -xzf $destination -C $temporaryRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to extract $destination"
        }

        $sourceDirectory = Get-ChildItem -LiteralPath $temporaryRoot -Directory -Recurse |
            Where-Object {
                $_.Name -eq 'NBS_ISCC' -and
                $_.Parent.Name -eq 'Dictionary'
            } |
            Select-Object -First 1
        if ($null -eq $sourceDirectory) {
            throw 'Color-Library archive did not contain the expected NBS_ISCC dictionary directory.'
        }

        $dictionaryNames = @('A.pm', 'B.pm', 'F.pm', 'H.pm', 'M.pm', 'P.pm', 'R.pm', 'RC.pm', 'S.pm', 'SC.pm', 'TC.pm')
        foreach ($name in $dictionaryNames) {
            Copy-Item -LiteralPath (Join-Path $sourceDirectory.FullName $name) `
                -Destination (Join-Path $dictionaryRoot $name) -Force
        }
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
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
        sourceQuality = $source.SourceQuality
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
    sourceQuality = 'UncertainProvenanceNumeric'
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
    sourceQuality = 'LexicalOnly'
}

$provenance |
    ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resolvedCache 'provenance.json') -Encoding utf8

Write-Output "Color taxonomy reference cache: $resolvedCache"
$provenance | Format-Table id, version, sha256
