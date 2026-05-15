#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Auto-updates digiWORK SmartHUB API from GitHub Releases.
    Run as a Scheduled Task on the API server.
#>

$ServiceName = "CompanyDirectoryApi"
$ApiDir      = "C:\Services\CompanyDirectory.Api"
$TempDir     = "C:\temp\api-update"
$Repo        = "digiwork/digiWORK-SmartHUB"
$LogFile     = "C:\Services\CompanyDirectory.Api\Logs\autoupdate.log"

function Write-Log {
    param([string]$Message)
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Write-Host $line
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    Add-Content $LogFile $line
}

Write-Log "Auto-update check started"

# Pobierz aktualną wersję z appsettings.json
try {
    $settings       = Get-Content "$ApiDir\appsettings.json" -Raw | ConvertFrom-Json
    $currentVersion = $settings.Updates.CurrentVersion
    Write-Log "Current version: $currentVersion"
} catch {
    Write-Log "ERROR: Cannot read appsettings.json — $_"
    exit 1
}

# Pobierz informacje o najnowszym release z GitHub
try {
    $release       = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest" -TimeoutSec 30
    $latestTag     = $release.tag_name          # np. "v1.1.0"
    $latestVersion = $latestTag.TrimStart('v')  # np. "1.1.0"
    Write-Log "Latest release: $latestVersion"
} catch {
    Write-Log "ERROR: Cannot reach GitHub API — $_"
    exit 1
}

# Porownaj wersje
if ([Version]$latestVersion -le [Version]$currentVersion) {
    Write-Log "API is up to date. No action needed."
    exit 0
}

Write-Log "Update available: $currentVersion -> $latestVersion. Starting update..."

# Znajdz asset z zipem API
$zipName = "digiWORK.SmartHUB.Api_$latestVersion.zip"
$asset   = $release.assets | Where-Object { $_.name -eq $zipName } | Select-Object -First 1
if (-not $asset) {
    Write-Log "ERROR: Asset '$zipName' not found in release $latestTag"
    exit 1
}

# Pobierz i rozpakuj
try {
    New-Item -ItemType Directory -Force $TempDir | Out-Null
    Write-Log "Downloading $zipName..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile "$TempDir\api.zip" -TimeoutSec 120
    Expand-Archive "$TempDir\api.zip" -DestinationPath "$TempDir\extracted" -Force
    Write-Log "Download complete"
} catch {
    Write-Log "ERROR: Download failed — $_"
    exit 1
}

# Zatrzymaj usluge, podmien exe, zaktualizuj wersje, uruchom
try {
    Write-Log "Stopping service..."
    Stop-Service $ServiceName -Force
    Start-Sleep -Seconds 2

    Copy-Item "$TempDir\extracted\CompanyDirectory.Api.exe" "$ApiDir\" -Force
    Write-Log "Executable replaced"

    $q       = [char]34
    $json    = Get-Content "$ApiDir\appsettings.json" -Raw
    $pattern = $q + 'CurrentVersion' + $q + '\s*:\s*' + $q + '[^' + $q + ']*' + $q
    $replace = $q + 'CurrentVersion' + $q + ': ' + $q + $latestVersion + $q
    $json    = $json -replace $pattern, $replace
    Set-Content "$ApiDir\appsettings.json" $json -Encoding UTF8
    Write-Log "Version updated in appsettings.json"

    Start-Service $ServiceName
    Write-Log "Service started"

    Start-Sleep -Seconds 3
    $svc = Get-Service $ServiceName
    if ($svc.Status -eq "Running") {
        Write-Log "SUCCESS: API updated to $latestVersion and running"
    } else {
        Write-Log "WARNING: Service status after update: $($svc.Status)"
    }
} catch {
    Write-Log "ERROR: Update failed — $_"
    try { Start-Service $ServiceName } catch {}
    exit 1
} finally {
    Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
}
