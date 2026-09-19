[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$readmePath = Join-Path $repositoryRoot 'README.md'
$evidenceRoot = Join-Path $repositoryRoot 'artifacts/branding'
$htmlPath = Join-Path $evidenceRoot 'readme-preview.html'
$payloadPath = Join-Path $evidenceRoot 'readme-preview-request.json'
$screenshotPath = Join-Path $evidenceRoot 'readme-preview.png'
[IO.Directory]::CreateDirectory($evidenceRoot) | Out-Null
if (Test-Path -LiteralPath $screenshotPath -PathType Leaf) {
    [IO.File]::Delete($screenshotPath)
}

$gh = Get-Command gh -ErrorAction Stop
$chromeCandidates = @(
    'C:\Program Files\Google\Chrome\Application\chrome.exe',
    'C:\Program Files (x86)\Google\Chrome\Application\chrome.exe'
)
$chrome = $chromeCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ($null -eq $chrome) {
    throw 'Google Chrome is unavailable for local README preview rendering.'
}

$markdown = [IO.File]::ReadAllText($readmePath)
$payload = @{
    text = $markdown
    mode = 'gfm'
    context = 'Samuelishe/Fovium'
} | ConvertTo-Json -Compress
[IO.File]::WriteAllText($payloadPath, $payload, [Text.UTF8Encoding]::new($false))
$rendered = (& $gh.Source api --method POST markdown --input $payloadPath) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($rendered)) {
    throw 'GitHub Markdown rendering failed.'
}

$baseUri = [Uri]::new(($repositoryRoot.TrimEnd('\') + '\')).AbsoluteUri
$html = @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <base href="$baseUri">
  <title>Fovium README preview</title>
  <style>
    :root { color-scheme: light; }
    body { margin: 0; background: #f6f8fa; color: #1f2328; font: 16px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
    .markdown-body { box-sizing: border-box; max-width: 1012px; margin: 32px auto; padding: 48px; background: #fff; border: 1px solid #d0d7de; border-radius: 8px; }
    h1, h2, h3 { line-height: 1.25; }
    h2 { margin-top: 28px; padding-bottom: .3em; border-bottom: 1px solid #d8dee4; }
    p { margin: 0 0 16px; }
    a { color: #0969da; text-decoration: none; }
    img { max-width: 100%; height: auto; }
    table { border-spacing: 0; border-collapse: collapse; display: block; width: max-content; max-width: 100%; overflow: auto; }
    th, td { padding: 6px 13px; border: 1px solid #d0d7de; }
    tr:nth-child(2n) { background: #f6f8fa; }
    code { padding: .2em .4em; border-radius: 6px; background: #eff1f3; }
    pre { padding: 16px; overflow: auto; border-radius: 6px; background: #f6f8fa; }
    pre code { padding: 0; background: transparent; }
  </style>
</head>
<body><article class="markdown-body">$rendered</article></body>
</html>
"@
[IO.File]::WriteAllText($htmlPath, $html, [Text.UTF8Encoding]::new($false))

$arguments = @(
    '--headless=new',
    '--disable-gpu',
    '--no-first-run',
    "--user-data-dir=$(Join-Path $evidenceRoot 'chrome-profile')",
    '--hide-scrollbars',
    '--window-size=1280,2400',
    "--screenshot=$screenshotPath",
    ([Uri]::new($htmlPath).AbsoluteUri)
)
& $chrome @arguments
$deadline = [DateTime]::UtcNow.AddSeconds(10)
while (-not (Test-Path -LiteralPath $screenshotPath -PathType Leaf) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 100
}
if (($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) -or -not (Test-Path -LiteralPath $screenshotPath -PathType Leaf)) {
    throw 'Chrome README screenshot rendering failed.'
}

Write-Host "README preview: $screenshotPath"
