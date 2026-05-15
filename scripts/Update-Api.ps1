#Requires -RunAsAdministrator

$ServiceName = "CompanyDirectoryApi"
$ApiDir      = "C:\Services\CompanyDirectory.Api"
$TempDir     = "C:\temp\api-update"
$Repo        = "digiwork/digiWORK-SmartHUB"
$LogFile     = "C:\Services\CompanyDirectory.Api\Logs\autoupdate.log"

function Write-Log([string]$msg) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $msg"
    Write-Host $line
    New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
    Add-Content -Path $LogFile -Value $line
}

Write-Log "Auto-update check started"

$settings = Get-Content "$ApiDir\appsettings.json" -Raw | ConvertFrom-Json
$currentVersion = $settings.Updates.CurrentVersion
Write-Log "Current version: $currentVersion"

$release = $null
try {
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest" -TimeoutSec 30
} catch {
    Write-Log "ERROR: GitHub API call failed"
    exit 1
}

$latestVersion = $release.tag_name.TrimStart('v')
Write-Log "Latest release: $latestVersion"

if ([Version]$latestVersion -le [Version]$currentVersion) {
    Write-Log "API is up to date. No action needed."
    exit 0
}

Write-Log "Update available: $currentVersion -> $latestVersion"

$q       = [char]34
$zipName = "digiWORK.SmartHUB.Api_$latestVersion.zip"
$asset   = $release.assets | Where-Object { $_.name -eq $zipName } | Select-Object -First 1
if (-not $asset) {
    Write-Log "ERROR: Asset $zipName not found in release"
    exit 1
}

New-Item -ItemType Directory -Force $TempDir | Out-Null

Write-Log "Downloading $zipName..."
try {
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile "$TempDir\api.zip" -TimeoutSec 120
    Expand-Archive "$TempDir\api.zip" -DestinationPath "$TempDir\extracted" -Force
} catch {
    Write-Log "ERROR: Download failed"
    exit 1
}
Write-Log "Download complete"

Write-Log "Stopping service..."
Stop-Service $ServiceName -Force
Start-Sleep -Seconds 2

Copy-Item "$TempDir\extracted\CompanyDirectory.Api.exe" "$ApiDir\" -Force
Write-Log "Executable replaced"

$pattern = $q + 'CurrentVersion' + $q + '\s*:\s*' + $q + '[^' + $q + ']*' + $q
$replace = $q + 'CurrentVersion' + $q + ': ' + $q + $latestVersion + $q
$json    = Get-Content "$ApiDir\appsettings.json" -Raw
$json    = $json -replace $pattern, $replace
Set-Content "$ApiDir\appsettings.json" $json -Encoding UTF8
Write-Log "Version updated to $latestVersion"

Start-Service $ServiceName
Start-Sleep -Seconds 3

$status = (Get-Service $ServiceName).Status
Write-Log "Service status: $status"

if ($status -eq "Running") {
    Write-Log "SUCCESS: API updated to $latestVersion"
} else {
    Write-Log "WARNING: Service did not start correctly"
}

Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
