param(
    [string]$Dotnet = 'dotnet',
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\publish')
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    & $Dotnet restore Aion2PetTracker.slnx --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed' }
    & $Dotnet test tests/Aion2PetTracker.Tests/Aion2PetTracker.Tests.csproj -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $Dotnet publish src/Aion2PetTracker.App/Aion2PetTracker.App.csproj -c Release -r win-x64 -p:RestoreLockedMode=true -o $Output --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Write-Host "Ready: $(Join-Path $Output 'Aion2PetTracker.exe')"
} finally { Pop-Location }
