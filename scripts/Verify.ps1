param([string]$Dotnet = 'dotnet', [string]$Archive = (Join-Path $PSScriptRoot '..\artifacts\release\Aion2-Pet-Tracker-Source.zip'))
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $repoRoot ('artifacts\verification\' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $Archive -DestinationPath $destination
$source = Join-Path $destination 'Aion2-Pet-Tracker-Source'
foreach ($line in Get-Content -LiteralPath (Join-Path $source 'SHA256SUMS.txt')) {
    $parts = $line -split '  ', 2
    if ($parts.Length -ne 2) { throw 'Malformed manifest entry' }
    $target = [IO.Path]::GetFullPath((Join-Path $source $parts[1]))
    if (!$target.StartsWith([IO.Path]::GetFullPath($source) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes source' }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $parts[0]) { throw "Hash mismatch: $($parts[1])" }
}
& (Join-Path $source 'scripts\Build.ps1') -Dotnet $Dotnet
if ($LASTEXITCODE -ne 0) { throw 'Extracted source verification failed' }
$originalExe = Join-Path $repoRoot 'artifacts\publish\Aion2PetTracker.exe'
$rebuiltExe = Join-Path $source 'artifacts\publish\Aion2PetTracker.exe'
if (Test-Path -LiteralPath $originalExe) {
    if ((Get-FileHash -LiteralPath $originalExe).Hash -ne (Get-FileHash -LiteralPath $rebuiltExe).Hash) {
        throw 'The extracted-source EXE differs from the original EXE'
    }
    Write-Host 'The extracted-source EXE is byte-for-byte identical to the original.'
}
Write-Host "Verified extracted source: $source"
