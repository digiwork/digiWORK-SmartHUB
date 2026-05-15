#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instaluje automatyczne aktualizacje API jako Scheduled Task.
    Uruchom raz na serwerze AP01 jako Administrator.
#>

$TaskName   = "digiWORK-SmartHUB-AutoUpdate"
$ScriptPath = "C:\Services\CompanyDirectory.Api\Update-Api.ps1"
$ScriptSrc  = Join-Path $PSScriptRoot "Update-Api.ps1"

# Skopiuj skrypt aktualizacji do folderu API
if (Test-Path $ScriptSrc) {
    Copy-Item $ScriptSrc $ScriptPath -Force
    Write-Host "Skrypt aktualizacji skopiowany do: $ScriptPath" -ForegroundColor Green
} else {
    Write-Error "Nie znaleziono Update-Api.ps1 w $PSScriptRoot"
    exit 1
}

# Usun istniejace zadanie jesli istnieje
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

# Utworz zaplanowane zadanie — codziennie o 03:00
$action  = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NonInteractive -ExecutionPolicy Bypass -File `"$ScriptPath`""

$trigger = New-ScheduledTaskTrigger -Daily -At "03:00"

$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
    -RestartCount 2 `
    -RestartInterval (New-TimeSpan -Minutes 5) `
    -StartWhenAvailable

$principal = New-ScheduledTaskPrincipal `
    -UserId "SYSTEM" `
    -LogonType ServiceAccount `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName  $TaskName `
    -Action    $action `
    -Trigger   $trigger `
    -Settings  $settings `
    -Principal $principal `
    -Description "Automatyczna aktualizacja digiWORK SmartHUB API z GitHub Releases" | Out-Null

Write-Host "Scheduled Task '$TaskName' utworzony." -ForegroundColor Green
Write-Host "Aktualizacje beda sprawdzane codziennie o 03:00." -ForegroundColor Cyan
Write-Host ""
Write-Host "Aby uruchomic recznie (test):" -ForegroundColor Yellow
Write-Host "  Start-ScheduledTask -TaskName '$TaskName'" -ForegroundColor White
Write-Host ""
Write-Host "Logi aktualizacji:" -ForegroundColor Yellow
Write-Host "  C:\Services\CompanyDirectory.Api\Logs\autoupdate.log" -ForegroundColor White
