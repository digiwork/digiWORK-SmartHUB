#Requires -Version 5
param(
    [Parameter(Mandatory)][string]$InstallDir,
    [Parameter(Mandatory)][string]$LdapServer,
    [Parameter(Mandatory)][int]   $LdapPort,
    [Parameter(Mandatory)][string]$BaseDn,
    [Parameter(Mandatory)][string]$SearchBase,
    [Parameter(Mandatory)][int]   $HttpPort,
    [Parameter(Mandatory)][string]$AdminLogins
)

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $InstallDir "appsettings.json"
Write-Output "Konfiguracja: $configPath"

$json = Get-Content $configPath -Raw | ConvertFrom-Json

# LDAP
$json.Ldap.Server     = $LdapServer
$json.Ldap.Port       = $LdapPort
$json.Ldap.UseSsl     = ($LdapPort -eq 636)
$json.Ldap.BaseDn     = $BaseDn
$json.Ldap.SearchBase = $SearchBase

# Logins administratorów (wiadomości + SMS)
$json.Messages.AdminLogins = $AdminLogins
$json.Sms.AdminLogins      = $AdminLogins

# Port HTTP (Kestrel)
$url = "http://0.0.0.0:$HttpPort"
if ($json.PSObject.Properties.Name -contains 'Urls') {
    $json.Urls = $url
} else {
    $json | Add-Member -Name 'Urls' -Value $url -MemberType NoteProperty
}

# Bezwzględna ścieżka do bazy wiadomości (przy pracy jako usługa CWD może być różny)
$dataDir = Join-Path $InstallDir "Data"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
$json.Messages.DatabasePath = Join-Path $dataDir "messages.db"

$json | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8
Write-Output "Konfiguracja zapisana."
