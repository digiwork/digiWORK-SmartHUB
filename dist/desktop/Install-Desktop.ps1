#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instaluje digiWORK SmartHUB na tym stanowisku.
    Uruchom jako Administrator.
#>

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot

Write-Host "=== Instalacja digiWORK SmartHUB ===" -ForegroundColor Cyan

# 1. Instalacja certyfikatu
$CerFile = Get-ChildItem $ScriptDir -Filter "*.cer" | Select-Object -First 1
if ($null -eq $CerFile) { throw "Nie znaleziono pliku .cer w $ScriptDir" }

Write-Host "[1/2] Instalacja certyfikatu: $($CerFile.Name)"
Import-Certificate -FilePath $CerFile.FullName `
    -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
Write-Host "      Certyfikat zainstalowany." -ForegroundColor Green

# 2. Instalacja MSIX
$MsixFile = Get-ChildItem $ScriptDir -Filter "*.msix" -Recurse | Select-Object -First 1
if ($null -eq $MsixFile) {
    $MsixFile = Get-ChildItem $ScriptDir -Filter "*.msixbundle" -Recurse | Select-Object -First 1
}
if ($null -eq $MsixFile) { throw "Nie znaleziono pliku MSIX w $ScriptDir" }

Write-Host "[2/2] Instalacja aplikacji: $($MsixFile.Name)"
Add-AppxPackage -Path $MsixFile.FullName -ForceApplicationShutdown
Write-Host "      Instalacja zakonczona." -ForegroundColor Green

Write-Host "`ndigiWORK SmartHUB zostal zainstalowany. Mozesz go uruchomic z menu Start." -ForegroundColor Cyan
