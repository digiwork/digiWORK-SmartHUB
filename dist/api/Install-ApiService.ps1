#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instaluje digiWORK SmartHUB API jako usluge Windows.
    Uruchom jako Administrator na maszynie serwerowej.
#>
param(
    [string]$ServiceName = "CompanyDirectoryApi",
    [string]$DisplayName = "digiWORK SmartHUB API"
)

$ScriptDir  = $PSScriptRoot
$ExePath    = Join-Path $ScriptDir "CompanyDirectory.Api.exe"

Write-Host "=== Instalacja digiWORK SmartHUB API jako usluga ===" -ForegroundColor Cyan

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Zatrzymywanie istniejacei uslugi..."
    Stop-Service -Name $ServiceName -Force
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Tworzenie uslugi: $ServiceName"
New-Service -Name $ServiceName `
            -DisplayName $DisplayName `
            -BinaryPathName $ExePath `
            -StartupType Automatic `
            -Description "digiWORK SmartHUB API — katalog pracowniczy" | Out-Null

Start-Service -Name $ServiceName
Write-Host "Usluga uruchomiona." -ForegroundColor Green
Write-Host "URL API: http://localhost:5112  (skonfiguruj w appsettings.json Desktop)" -ForegroundColor Yellow
