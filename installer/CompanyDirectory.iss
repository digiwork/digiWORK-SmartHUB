; CompanyDirectory — Inno Setup 6 installer script
; Wymaga: Inno Setup 6.x (iscc.exe)

#define AppName      "digiWORK SmartHUB"
#define AppVersion   "1.0.0"
#define AppPublisher "AGROAS sp. z o.o."
#define AppPkgName   "B5636A49-99A7-463E-A93B-CC92F074F3BF"

#define MsixDir      "..\dist\desktop\CompanyDirectory.Desktop_1.0.0.0_x64_Test"
#define MsixFile     "CompanyDirectory.Desktop_1.0.0.0_x64.msix"
#define CerFile      "CompanyDirectory.Desktop_1.0.0.0_x64.cer"
#define WinRtMsix    "Dependencies\x64\Microsoft.WindowsAppRuntime.2.msix"

; ── Konfiguracja ogólna ──────────────────────────────────────────────────────
[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppId={{B5636A49-99A7-463E-A93B-CC92F074F3BF}
DefaultDirName={commonappdata}\digiWORK SmartHUB\Installer
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=digiWORK_SmartHUB_{#AppVersion}_Setup
SetupIconFile=..\src\CompanyDirectory.Desktop\Assets\AppIcon.ico
WizardStyle=modern
WizardSizePercent=120,110
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=no
UninstallDisplayName={#AppName}
UninstallDisplayIcon={uninstallexe}
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}
VersionInfoCopyright=Copyright © 2025 {#AppPublisher}
MinVersion=10.0.17763

; ── Języki ──────────────────────────────────────────────────────────────────
[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

; ── Komunikaty PL ────────────────────────────────────────────────────────────
[Messages]
polish.WelcomeLabel1=Witaj w instalatorze aplikacji [bold]{#AppName}[/bold]
polish.WelcomeLabel2=Ten kreator przeprowadzi Cię przez instalację aplikacji {#AppName} {#AppVersion}.%n%nAplikacja wymaga systemu Windows 10 w wersji 1809 lub nowszego.%n%nAby kontynuować, kliknij [bold]Dalej[/bold].
polish.FinishedHeadingLabel=Instalacja [bold]{#AppName}[/bold] zakończona
polish.FinishedLabelNoIcons=Aplikacja {#AppName} została pomyślnie zainstalowana na Twoim komputerze.%n%nMożesz ją uruchomić z menu Start.

; ── Pliki do spakowania ──────────────────────────────────────────────────────
[Files]
; Skrypt instalacyjny PS (usuwany po instalacji)
Source: "install-helper.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "launcher.ps1";       DestDir: "{tmp}"; Flags: deleteafterinstall

; Certyfikat podpisujący
Source: "{#MsixDir}\{#CerFile}"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Windows App Runtime (x64)
Source: "{#MsixDir}\{#WinRtMsix}"; DestDir: "{tmp}"; Flags: deleteafterinstall

; Główny pakiet MSIX
Source: "{#MsixDir}\{#MsixFile}"; DestDir: "{tmp}"; Flags: deleteafterinstall

; ── Wykonaj podczas instalacji ───────────────────────────────────────────────
[Run]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{tmp}\install-helper.ps1"" -CerFile ""{tmp}\{#CerFile}"" -RuntimeMsix ""{tmp}\Microsoft.WindowsAppRuntime.2.msix"" -AppMsix ""{tmp}\{#MsixFile}"""; \
  StatusMsg: "Instalacja certyfikatu i aplikacji..."; \
  Flags: runhidden waituntilterminated

; Opcja uruchomienia po instalacji (checkbox na stronie Finish)
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{tmp}\launcher.ps1"""; \
  Description: "Uruchom {#AppName}"; \
  Flags: postinstall runhidden nowait skipifsilent

; ── Wykonaj podczas deinstalacji ─────────────────────────────────────────────
[UninstallRun]
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Get-AppxPackage -Name '{#AppPkgName}' | Remove-AppxPackage"""; \
  StatusMsg: "Usuwanie aplikacji digiWORK SmartHUB..."; \
  RunOnceId: "RemoveMsix"; \
  Flags: runhidden waituntilterminated
