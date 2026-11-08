[CmdletBinding()]
param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    dotnet restore FreeNetworkMonitor.sln
    if ($LASTEXITCODE -ne 0) { throw 'Échec de restauration.' }
    dotnet build FreeNetworkMonitor.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Échec de compilation.' }
    dotnet test tests/FreeNetworkMonitor.Tests -c $Configuration --no-build --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/tests
    if ($LASTEXITCODE -ne 0) { throw 'Échec des tests.' }
    dotnet publish src/FreeNetworkMonitor.App -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/publish/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Échec de publication Windows.' }
    Copy-Item README.md,THIRD_PARTY_NOTICES.md artifacts/publish/win-x64/
    if (Test-Path artifacts/FreeNetworkMonitor-win-x64.zip) { Remove-Item artifacts/FreeNetworkMonitor-win-x64.zip }
    Compress-Archive artifacts/publish/win-x64/* artifacts/FreeNetworkMonitor-win-x64.zip
    Write-Host 'Publication : artifacts/publish/win-x64/FreeNetworkMonitor.App.exe'
} finally { Pop-Location }
