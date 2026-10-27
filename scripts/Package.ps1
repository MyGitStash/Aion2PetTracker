param([string]$Output = (Join-Path $PSScriptRoot '..\artifacts\release'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$publishExe = Join-Path $repoRoot 'artifacts\publish\Aion2PetTracker.exe'
if (!(Test-Path -LiteralPath $publishExe)) { throw 'Run scripts/Build.ps1 first' }
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-Manifest([string]$Root) {
    $fullRoot = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $lines = Get-ChildItem -LiteralPath $fullRoot -Recurse -File |
        Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
            $rel = $_.FullName.Substring($fullRoot.Length + 1).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $rel"
        }
    [IO.File]::WriteAllLines((Join-Path $fullRoot 'SHA256SUMS.txt'), [string[]]$lines, [Text.UTF8Encoding]::new($false))
}
function Write-Zip([string]$Folder, [string]$Destination) {
    # Replacing a named generated archive is deliberate; source and runtime data are never removed.
    if (Test-Path -LiteralPath $Destination) { Remove-Item -LiteralPath $Destination }
    [IO.Compression.ZipFile]::CreateFromDirectory($Folder, $Destination, [IO.Compression.CompressionLevel]::Optimal, $true)
}

Push-Location $repoRoot
try {
    $dirty = & git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Commit the complete source before packaging' }
    $commit = (& git rev-parse HEAD).Trim()
    $tagCommit = (& git rev-parse 'release-1^{commit}').Trim()
    if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $commit) { throw 'release-1 must point to the source being packaged' }
    New-Item -Path $Output -ItemType Directory -Force | Out-Null
    $outputRoot = [IO.Path]::GetFullPath($Output)
    $stage = Join-Path $repoRoot ('artifacts\packages\' + [Guid]::NewGuid().ToString('N'))
    $source = Join-Path $stage 'Aion2-Pet-Tracker-Source'
    $portable = Join-Path $stage 'portable\Aion2-Pet-Tracker'
    $review = Join-Path $stage 'review\Aion2-Pet-Tracker'
    foreach ($folder in @($source, $portable, $review)) { New-Item -Path $folder -ItemType Directory -Force | Out-Null }
    $files = & git ls-files
    foreach ($rel in $files) {
        if ($rel -match '(^|/)(Daten|Data|bin|obj|artifacts|\.git)(/|$)' -or $rel -match '\.(pcapng?|jsonl|user)$') {
            throw "Private/generated file unexpectedly tracked: $rel"
        }
        $dest = Join-Path $source $rel
        New-Item -Path (Split-Path $dest -Parent) -ItemType Directory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $rel) -Destination $dest
    }
    Write-Manifest $source
    Copy-Item -LiteralPath $publishExe -Destination (Join-Path $portable 'Aion2PetTracker.exe')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $portable
    Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination (Join-Path $portable 'README.txt')
    foreach ($name in @('docs', 'licenses')) { Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $portable -Recurse }
    Write-Manifest $portable
    Copy-Item -Path (Join-Path $portable '*') -Destination $review -Recurse
    Copy-Item -LiteralPath $source -Destination (Join-Path $review 'source') -Recurse
    $bundle = Join-Path $outputRoot 'Aion2-Pet-Tracker.bundle'
    & git bundle create $bundle HEAD --branches --tags
    if ($LASTEXITCODE -ne 0) { throw 'Git bundle creation failed' }
    & git bundle verify $bundle
    if ($LASTEXITCODE -ne 0) { throw 'Git bundle verification failed' }
    Copy-Item -LiteralPath $bundle -Destination $review
    $previews = Join-Path $repoRoot 'artifacts\previews'
    if (Test-Path -LiteralPath $previews) { Copy-Item -LiteralPath $previews -Destination (Join-Path $review 'preview') -Recurse }
    Write-Manifest $review
    Write-Zip $portable (Join-Path $outputRoot 'Aion2-Pet-Tracker.zip')
    Write-Zip $source (Join-Path $outputRoot 'Aion2-Pet-Tracker-Source.zip')
    Write-Zip $review (Join-Path $outputRoot 'Aion2-Pet-Tracker-Review.zip')
    $hashes = Get-ChildItem -LiteralPath $outputRoot -File | Where-Object { $_.Extension -in '.zip', '.bundle' } |
        Sort-Object Name | ForEach-Object { "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)" }
    [IO.File]::WriteAllLines((Join-Path $outputRoot 'SHA256SUMS.txt'), [string[]]$hashes, [Text.UTF8Encoding]::new($false))
    [ordered]@{ product = 'Aion2 Pet Tracker'; release = 1; source_commit = $commit; tag = 'release-1'; npcap_bundled = $false; personal_data_included = $false } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'release.json') -Encoding utf8
    Write-Host "Archives ready: $outputRoot"
} finally { Pop-Location }
