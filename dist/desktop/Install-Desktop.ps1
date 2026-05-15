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

# 2. Instalacja przez .appinstaller (rejestruje zrodlo aktualizacji — wymagane do auto-update)
$AppInstaller = Get-ChildItem $ScriptDir -Filter "*.appinstaller" | Select-Object -First 1
if ($null -eq $AppInstaller) { throw "Nie znaleziono pliku .appinstaller w $ScriptDir" }

Write-Host "[2/2] Instalacja aplikacji: $($AppInstaller.Name)"
Write-Host "      Pobieranie pakietu z GitHub (wymagany internet)..."
Add-AppxPackage -Path $AppInstaller.FullName -ForceApplicationShutdown
Write-Host "      Instalacja zakonczona." -ForegroundColor Green

Write-Host "`ndigiWORK SmartHUB zostal zainstalowany. Mozesz go uruchomic z menu Start." -ForegroundColor Cyan
Write-Host "Auto-aktualizacje beda sprawdzane przy kazdym uruchomieniu aplikacji." -ForegroundColor Cyan
