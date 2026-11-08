$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $env:TEMP 'FreeNetworkMonitor-install-test'
$installer = Join-Path $root 'artifacts/installer/FreeNetworkMonitor-Setup-1.0.0.exe'
$log = Join-Path $root 'artifacts/installer-test.log'
$installed = Start-Process $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$destination`"", "/LOG=`"$log`"") -Wait -PassThru
if ($installed.ExitCode -ne 0) { throw "Installation échouée : $($installed.ExitCode)" }
if (-not (Test-Path "$destination/FreeNetworkMonitor.App.exe")) { throw 'Application installée absente.' }
# Le stockage des mesures ne fait jamais partie du dossier programme.
$uninstalled = Start-Process "$destination/unins000.exe" -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
if ($uninstalled.ExitCode -ne 0) { throw "Désinstallation échouée : $($uninstalled.ExitCode)" }
if (Test-Path "$destination/FreeNetworkMonitor.App.exe") { throw 'Exécutable encore présent après désinstallation.' }
Write-Host 'Installation et désinstallation silencieuses réussies.'
