param(
    [Parameter(Mandatory)][string]$CerFile,
    [Parameter(Mandatory)][string]$RuntimeMsix,
    [Parameter(Mandatory)][string]$AppMsix
)

$ErrorActionPreference = 'Stop'
$LogPath = "C:\Windows\Temp\CompanyDirectory_install.log"
Start-Transcript -Path $LogPath -Append -Force

try {
    Write-Output "=== CompanyDirectory Install Helper ==="
    Write-Output "Date:       $(Get-Date)"
    Write-Output "PSVersion:  $($PSVersionTable.PSVersion)"
    Write-Output "Is64bit PS: $([Environment]::Is64BitProcess)"
    Write-Output "RunAs:      $([Security.Principal.WindowsIdentity]::GetCurrent().Name)"
    Write-Output ""

    # 1. Certificate -> LocalMachine\TrustedPeople
    Write-Output "[1/3] Certificate: $CerFile"
    & certutil.exe -f -addstore TrustedPeople $CerFile
    if ($LASTEXITCODE -ne 0) { throw "certutil failed (exit $LASTEXITCODE)" }
    Write-Output "Certificate OK"
    Write-Output ""

    # 2. Windows App Runtime
    Write-Output "[2/3] Windows App Runtime: $RuntimeMsix"
    try {
        Add-AppxPackage -Path $RuntimeMsix -ErrorAction Stop
        Write-Output "Windows App Runtime OK"
    } catch {
        Write-Output "Windows App Runtime skipped: already installed or newer version present"
    }
    Write-Output ""

    # 3. Uninstall previous version if present
    Write-Output "[3/4] Removing previous version (if installed)..."
    $existing = Get-AppxPackage -Name "B5636A49-99A7-463E-A93B-CC92F074F3BF" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Output "     Found: $($existing.PackageFullName)"
        Remove-AppxPackage -Package $existing.PackageFullName -ErrorAction Stop
        Write-Output "     Previous version removed."
    } else {
        Write-Output "     No previous version found."
    }
    Write-Output ""

    # 4. Main MSIX package
    Write-Output "[4/4] CompanyDirectory MSIX: $AppMsix"
    Add-AppxPackage -Path $AppMsix -ForceApplicationShutdown
    Write-Output "CompanyDirectory OK"
    Write-Output ""

    Write-Output "=== Install completed successfully ==="
} catch {
    Write-Output ""
    Write-Output "=== INSTALL ERROR ==="
    Write-Output $_.Exception.Message
    Write-Output $_.ScriptStackTrace
} finally {
    Stop-Transcript
}
