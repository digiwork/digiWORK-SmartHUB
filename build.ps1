#Requires -Version 7
<#
.SYNOPSIS
    Buduje CompanyDirectory do dystrybucji.
    Tworzy: dist/desktop/ (MSIX + cert) i dist/api/ (self-contained exe)

.PARAMETER CertPassword
    Haslo do certyfikatu PFX (domyslnie: CompanyDir2025)

.EXAMPLE
    .\build.ps1
    .\build.ps1 -CertPassword "MojeHaslo"
#>
param(
    [string]$CertPassword = "CompanyDir2025"
)

$ErrorActionPreference = "Stop"
$Root    = $PSScriptRoot
$DistDir = Join-Path $Root "dist"

Write-Host "`n=== digiWORK SmartHUB — Build dystrybucji ===" -ForegroundColor Cyan

# ── Katalogi wyjściowe ───────────────────────────────────────────────────────
$DesktopDist = Join-Path $DistDir "desktop"
$ApiDist     = Join-Path $DistDir "api"
New-Item -ItemType Directory -Force -Path $DesktopDist | Out-Null
New-Item -ItemType Directory -Force -Path $ApiDist     | Out-Null

# ── 1. Certyfikat self-signed ────────────────────────────────────────────────
$PfxPath = Join-Path $Root "CompanyDirectory.pfx"
$CerPath = Join-Path $DesktopDist "CompanyDirectory.cer"

Write-Host "`n[1/4] Certyfikat podpisujacy..." -ForegroundColor Yellow

function New-AppCertificate {
    param([string]$PfxOut, [string]$CerOut, [string]$Password)

    Add-Type -AssemblyName System.Security

    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    $req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        "CN=AppPublisher",
        $rsa,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    # Code Signing EKU
    $oids = [System.Security.Cryptography.OidCollection]::new()
    $oids.Add([System.Security.Cryptography.Oid]::new("1.3.6.1.5.5.7.3.3")) | Out-Null
    $req.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $true)) | Out-Null

    # Basic constraints (not a CA)
    $req.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $false)) | Out-Null

    $cert = $req.CreateSelfSigned(
        [DateTimeOffset]::Now.AddDays(-1),
        [DateTimeOffset]::Now.AddYears(5))

    # Export PFX
    $pfxBytes = $cert.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $Password)
    [System.IO.File]::WriteAllBytes($PfxOut, $pfxBytes)

    # Export DER .cer (public part only)
    $cerBytes = $cert.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    [System.IO.File]::WriteAllBytes($CerOut, $cerBytes)

    # Re-import from PFX with PersistKeySet so signtool can access the private key
    $persistCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $pfxBytes, $Password,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        "My", [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $store.Add($persistCert)
    $store.Close()

    return $persistCert.Thumbprint
}

if (-not (Test-Path $PfxPath)) {
    Write-Host "     Tworzenie nowego certyfikatu (CN=AppPublisher)..."
    $null = New-AppCertificate -PfxOut $PfxPath -CerOut $CerPath -Password $CertPassword
    Write-Host "     Certyfikat zapisany: $PfxPath" -ForegroundColor Green
} else {
    Write-Host "     Uzywam istniejacego certyfikatu: $PfxPath"
    $pfxBytes = [System.IO.File]::ReadAllBytes($PfxPath)
    $existingCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $pfxBytes, $CertPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet)
    $cerBytes = $existingCert.Export(
        [System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    [System.IO.File]::WriteAllBytes($CerPath, $cerBytes)
}

# ── 2. Build Desktop (MSIX x64) ─────────────────────────────────────────────
Write-Host "`n[2/5] Build Desktop (MSIX, x64, Release)..." -ForegroundColor Yellow

$DesktopProj = Join-Path $Root "src\CompanyDirectory.Desktop\CompanyDirectory.Desktop.csproj"

dotnet build $DesktopProj `
    --configuration Release `
    -p:Platform=x64 `
    -p:RuntimeIdentifier=win-x64 `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxPackageDir=$DesktopDist\ `
    -p:GenerateAppxPackageOnBuild=true

if ($LASTEXITCODE -ne 0) { throw "Build Desktop nie powiodl sie" }

# Skopiuj .cer do folderu _Test zeby Inno Setup mógl go znalezc
$TestDir = Get-ChildItem $DesktopDist -Directory | Where-Object { $_.Name -match "_Test$" } | Select-Object -First 1
if ($TestDir) {
    $MsixInTest = Get-ChildItem $TestDir.FullName -Filter "*.msix" | Select-Object -First 1
    if ($MsixInTest) {
        $CerDestPath = Join-Path $TestDir.FullName ($MsixInTest.BaseName + ".cer")
        Copy-Item $CerPath $CerDestPath -Force
    }
}

# Znajdz wynikowy MSIX i podpisz go bezposrednio signtoolem (PFX z dysku — niezawodna metoda)
$Msix = Get-ChildItem $DesktopDist -Filter "*.msix" -Recurse | Select-Object -First 1
if ($null -eq $Msix) {
    $Msix = Get-ChildItem $DesktopDist -Filter "*.msixbundle" -Recurse | Select-Object -First 1
}
if ($null -ne $Msix) {
    Write-Host "     MSIX: $($Msix.FullName)" -ForegroundColor Green

    $SignTool = @(
        Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
            -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue
        Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" `
            -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue
    ) | Where-Object { $_.FullName -match "\\x64\\" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $SignTool) {
        throw "signtool.exe nie znaleziony — zainstaluj Windows SDK lub dodaj NuGet 'Microsoft.Windows.SDK.BuildTools'"
    }
    Write-Host "     Podpisywanie MSIX ($($SignTool.FullName))..." -ForegroundColor Yellow
    & $SignTool.FullName sign /fd SHA256 /f $PfxPath /p $CertPassword $Msix.FullName
    if ($LASTEXITCODE -ne 0) { throw "Podpisywanie MSIX nie powiodlo sie (signtool exit $LASTEXITCODE)" }
    Write-Host "     MSIX podpisany OK" -ForegroundColor Green
} else {
    Write-Warning "Nie znaleziono pliku MSIX w $DesktopDist — sprawdz recznie folder bin/"
}

# ── 3. Publish API (self-contained, win-x64) ─────────────────────────────────
Write-Host "`n[3/5] Publish API (self-contained, win-x64)..." -ForegroundColor Yellow

$ApiProj = Join-Path $Root "src\CompanyDirectory.Api\CompanyDirectory.Api.csproj"

dotnet publish $ApiProj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $ApiDist

if ($LASTEXITCODE -ne 0) { throw "Publish API nie powiodl sie" }

Write-Host "     API opublikowane: $ApiDist" -ForegroundColor Green

# ── 4. Pliki pomocnicze ──────────────────────────────────────────────────────
Write-Host "`n[4/5] Generowanie skryptow instalacyjnych..." -ForegroundColor Yellow

# Skrypt instalatora dla maszyny docelowej
$InstallScript = @'
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
'@

$InstallScript | Set-Content (Join-Path $DesktopDist "Install-Desktop.ps1") -Encoding UTF8

# Skrypt uruchamiający API (dla maszyny serwerowej)
$ApiStartScript = @'
@echo off
echo Uruchamianie digiWORK SmartHUB API...
echo Nacisnij Ctrl+C aby zatrzymac.
echo.
cd /d "%~dp0"
CompanyDirectory.Api.exe
'@

$ApiStartScript | Set-Content (Join-Path $ApiDist "start.bat") -Encoding ASCII

# Skrypt instalacji API jako Windows Service
$ApiServiceScript = @'
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
'@

$ApiServiceScript | Set-Content (Join-Path $ApiDist "Install-ApiService.ps1") -Encoding UTF8

# ── 5. Inno Setup — kompilacja instalatora ────────────────────────────────────
Write-Host "`n[5/5] Kompilacja instalatora Inno Setup..." -ForegroundColor Yellow

$IsccPath = "C:\Program Files (x86)\Inno Setup 6\iscc.exe"
$IssFile  = Join-Path $Root "installer\CompanyDirectory.iss"
$InstallerDir = Join-Path $DistDir "installer"
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null

if (Test-Path $IsccPath) {
    # Desktop installer
    & $IsccPath $IssFile
    if ($LASTEXITCODE -ne 0) { throw "Kompilacja instalatora Desktop nie powiodla sie" }

    # API installer
    $IssApiFile = Join-Path $Root "installer\CompanyDirectory.Api.iss"
    & $IsccPath $IssApiFile
    if ($LASTEXITCODE -ne 0) { throw "Kompilacja instalatora API nie powiodla sie" }

    Get-ChildItem $InstallerDir -Filter "*.exe" | ForEach-Object {
        Write-Host "     Instalator: $($_.Name)  [$([Math]::Round($_.Length/1MB,1)) MB]" -ForegroundColor Green
    }
} else {
    Write-Warning "Inno Setup nie znaleziony w $IsccPath — pomijam kompilacje instalatorow."
    Write-Warning "Zainstaluj Inno Setup 6 i uruchom ponownie, lub skompiluj recznie:"
    Write-Warning "  iscc installer\CompanyDirectory.iss"
    Write-Warning "  iscc installer\CompanyDirectory.Api.iss"
}

# ── Podsumowanie ─────────────────────────────────────────────────────────────
$MsixFolder = Get-ChildItem $DesktopDist -Directory | Where-Object { $_.Name -match "\.msix$|_Test$" } | Select-Object -First 1
$MsixRelative = if ($MsixFolder) { "dist/desktop/$($MsixFolder.Name)/" } else { "dist/desktop/*_Test/" }

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host " BUILD ZAKONCZONY POMYSLNIE" -ForegroundColor Green
Write-Host "========================================="
Write-Host ""
Write-Host "  DESKTOP — skopiuj caly folder na stanowisko testowe:" -ForegroundColor White
Write-Host "    $MsixRelative" -ForegroundColor Cyan
Write-Host "    Zawiera: *.msix, *.cer, Install.ps1 (uruchom jako Admin)"
Write-Host ""
Write-Host "  API — skopiuj caly folder na serwer z AD:" -ForegroundColor White
Write-Host "    dist/api/" -ForegroundColor Cyan
Write-Host "    CompanyDirectory.Api.exe  — serwer API"
Write-Host "    appsettings.json          — SKONFIGURUJ LDAP przed uruchomieniem"
Write-Host "    start.bat                 — szybkie uruchomienie"
Write-Host "    Install-ApiService.ps1    — instalacja jako usluga Windows (Admin)"
Write-Host ""
Write-Host "  INSTALACJA NA STANOWISKU TESTOWYM (dwie opcje):" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Opcja A — Instalator EXE (zalecane dla uzytkownika konc.):" -ForegroundColor Green
Write-Host "    dist/installer/digiWORK_SmartHUB_1.0.0_Setup.exe" -ForegroundColor Cyan
Write-Host "    → uruchom jako Administrator, kreator zrobi reszte"
Write-Host ""
Write-Host "  Opcja B — Reczna instalacja (dla technikow IT):" -ForegroundColor Yellow
Write-Host "    Skopiuj folder: $MsixRelative" -ForegroundColor Yellow
Write-Host "    Uruchom Install.ps1 jako Administrator" -ForegroundColor Yellow
Write-Host ""
Write-Host "  WAZNE: W dist/api/appsettings.json skonfiguruj sekcje Ldap" -ForegroundColor Yellow
Write-Host "         Po instalacji ustaw BaseUrl w Ustawieniach aplikacji na IP serwera API" -ForegroundColor Yellow
Write-Host ""
