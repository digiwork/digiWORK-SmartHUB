#Requires -Version 5
param(
    [Parameter(Mandatory)][string]$InstallDir,
    [string]$ServiceName = "CompanyDirectoryApi",
    [string]$DisplayName = "digiWORK SmartHUB API",
    [int]   $Port        = 5112
)

$ErrorActionPreference = 'Stop'
$exePath = Join-Path $InstallDir "CompanyDirectory.Api.exe"

# Usuń istniejącą usługę (scenariusz aktualizacji)
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Output "Zatrzymywanie istniejącej usługi '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Zainstaluj usługę Windows
Write-Output "Tworzenie usługi: $ServiceName"
New-Service -Name $ServiceName `
            -DisplayName $DisplayName `
            -BinaryPathName "`"$exePath`"" `
            -StartupType Automatic `
            -Description "digiWORK SmartHUB API — katalog pracowniczy"

Start-Service -Name $ServiceName
Write-Output "Usługa uruchomiona: $ServiceName"

# Reguła firewall (wejście TCP na danym porcie)
$ruleName = "digiWORK SmartHUB API (port $Port)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if (-not $existing) {
    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction  Inbound `
        -Protocol   TCP `
        -LocalPort  $Port `
        -Action     Allow `
        -Profile    Any | Out-Null
    Write-Output "Reguła firewalla dodana: $ruleName"
} else {
    Write-Output "Reguła firewalla już istnieje: $ruleName"
}
