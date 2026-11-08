[CmdletBinding()]
param([string]$IsccPath = '', [switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { throw 'Inno Setup nécessite Windows.' }
if (-not $SkipPublish) { & "$PSScriptRoot/publish-win-x64.ps1" }
if (-not (Test-Path "$root/artifacts/publish/win-x64/FreeNetworkMonitor.App.exe")) { throw 'Exécutable publié absent.' }
if (-not $IsccPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
    else { $IsccPath = "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe" }
}
if (-not (Test-Path $IsccPath)) { throw 'Installer Inno Setup 6 depuis https://jrsoftware.org/isinfo.php, puis fournir -IsccPath si nécessaire.' }
& $IsccPath "$PSScriptRoot/installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Échec Inno Setup.' }
Write-Host 'Installateur : artifacts/installer/FreeNetworkMonitor-Setup-1.0.0.exe'
