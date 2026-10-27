param([Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install the official GitHub CLI and run gh auth login first.' }
& gh auth status
if ($LASTEXITCODE -ne 0) { throw 'An authenticated GitHub account is required.' }
$assets = @('Aion2-Pet-Tracker.zip', 'Aion2-Pet-Tracker-Source.zip', 'Aion2-Pet-Tracker-Review.zip', 'Aion2-Pet-Tracker.bundle', 'SHA256SUMS.txt', 'release.json') |
    ForEach-Object { Join-Path $repoRoot ('artifacts\release\' + $_) }
foreach ($asset in $assets) { if (!(Test-Path -LiteralPath $asset)) { throw "Prepare the release archive first: $asset" } }
Push-Location $repoRoot
try {
    if (& git status --porcelain) { throw 'The repository must be clean before publishing.' }
    $origin = & git remote get-url origin 2>$null
    if ($origin) { throw 'An origin already exists; inspect it and publish to that repository manually.' }
    & gh repo create $Repository --public --source . --remote origin --push --description 'Aion2 Pet Tracker: transparent pet soul and Cloud Merchant overlays, with reviewable source'
    if ($LASTEXITCODE -ne 0) { throw 'Repository creation/push failed; no release was created.' }
    & git push origin release-1
    if ($LASTEXITCODE -ne 0) { throw 'Release tag push failed.' }
    & gh release create release-1 @assets --repo $Repository --verify-tag --title 'Release 1' --notes-file docs/RELEASE-1.md
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed; inspect the repository before retrying.' }
} finally { Pop-Location }
