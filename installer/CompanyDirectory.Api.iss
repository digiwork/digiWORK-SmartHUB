; CompanyDirectory API — Inno Setup 6 installer script
; Wymaga: Inno Setup 6.x (iscc.exe)

#define AppName     "digiWORK SmartHUB API"
#define AppVersion  "1.0.0"
#define AppPublisher "AGROAS sp. z o.o."
#define ServiceName "CompanyDirectoryApi"

; ── Konfiguracja ogólna ──────────────────────────────────────────────────────
[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppId={{CD-API-B5636A49-99A7-463E-A93B-CC92F074F3BF}
DefaultDirName={autopf}\digiWORK SmartHUB\API
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=digiWORK_SmartHUB_API_{#AppVersion}_Setup
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
MinVersion=10.0.17763

; ── Języki ──────────────────────────────────────────────────────────────────
[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

; ── Komunikaty PL ────────────────────────────────────────────────────────────
[Messages]
polish.WelcomeLabel1=Witaj w instalatorze [bold]{#AppName}[/bold]
polish.WelcomeLabel2=Ten kreator zainstaluje serwer API aplikacji digiWORK SmartHUB jako usługę systemu Windows.%n%nAPI musi być zainstalowane na maszynie z dostępem do kontrolera domeny Active Directory.%n%nAby kontynuować, kliknij [bold]Dalej[/bold].
polish.FinishedHeadingLabel=Instalacja [bold]{#AppName}[/bold] zakończona
polish.FinishedLabelNoIcons=Serwer API został pomyślnie zainstalowany i uruchomiony jako usługa Windows.%n%nW aplikacji desktopowej ustaw API Base URL na adres tego serwera:%n    http://<adres-IP>:{code:GetHttpPort}

; ── Rejestr — przechowujemy nazwę reguły firewalla do deinstalacji ───────────
[Registry]
Root: HKLM; Subkey: "SOFTWARE\digiWORK SmartHUB\API"; \
  ValueType: string; ValueName: "FirewallRuleName"; \
  ValueData: "digiWORK SmartHUB API (port {code:GetHttpPort})"; \
  Flags: createvalueifdoesntexist uninsdeletekeyifempty uninsdeletevalue

; ── Pliki do zainstalowania ──────────────────────────────────────────────────
[Files]
; API
Source: "..\dist\api\CompanyDirectory.Api.exe";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\api\appsettings.json";             DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\api\appsettings.Production.json";  DestDir: "{app}"; Flags: ignoreversion

; Skrypty pomocnicze (usuwane po zakończeniu instalacji)
Source: "api-configure.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "api-service.ps1";   DestDir: "{tmp}"; Flags: deleteafterinstall

; ── Wykonaj podczas instalacji ───────────────────────────────────────────────
[Run]
; 1. Zapisz konfigurację LDAP + port do appsettings.json
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{tmp}\api-configure.ps1"" -InstallDir ""{app}"" -LdapServer ""{code:GetLdapServer}"" -LdapPort ""{code:GetLdapPort}"" -BaseDn ""{code:GetBaseDn}"" -SearchBase ""{code:GetSearchBase}"" -HttpPort ""{code:GetHttpPort}"" -AdminLogins ""{code:GetAdminLogins}"""; \
  StatusMsg: "Zapisywanie konfiguracji LDAP..."; \
  Flags: runhidden waituntilterminated

; 2. Zainstaluj i uruchom usługę Windows + reguła firewalla
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{tmp}\api-service.ps1"" -InstallDir ""{app}"" -ServiceName ""{#ServiceName}"" -DisplayName ""{#AppName}"" -Port ""{code:GetHttpPort}"""; \
  StatusMsg: "Instalacja usługi Windows i reguły firewalla..."; \
  Flags: runhidden waituntilterminated

; ── Wykonaj podczas deinstalacji ─────────────────────────────────────────────
[UninstallRun]
; Zatrzymaj i usuń usługę
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Stop-Service -Name '{#ServiceName}' -Force -ErrorAction SilentlyContinue"""; \
  RunOnceId: "StopService"; \
  Flags: runhidden waituntilterminated

Filename: "sc.exe"; \
  Parameters: "delete {#ServiceName}"; \
  RunOnceId: "DeleteService"; \
  Flags: runhidden waituntilterminated

; Usuń regułę firewalla (nazwa pobrana z rejestru)
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ""Remove-NetFirewallRule -DisplayName ((Get-ItemProperty 'HKLM:\SOFTWARE\digiWORK SmartHUB\API' -ErrorAction SilentlyContinue).FirewallRuleName) -ErrorAction SilentlyContinue"""; \
  RunOnceId: "RemoveFirewall"; \
  Flags: runhidden waituntilterminated

; ── Logika kreatora ──────────────────────────────────────────────────────────
[Code]
var
  LdapPage:    TInputQueryWizardPage;
  ServicePage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  // ── Strona 1: konfiguracja LDAP ──────────────────────────────────────────
  LdapPage := CreateInputQueryPage(wpSelectDir,
    'Konfiguracja Active Directory',
    'Podaj dane serwera LDAP / Active Directory',
    'Wartości możesz zmienić później w pliku: {app}\appsettings.json');

  LdapPage.Add('Serwer LDAP (hostname lub IP):', False);
  LdapPage.Add('Port  (636 = LDAPS/SSL,  389 = bez szyfrowania):', False);
  LdapPage.Add('Base DN  (np. DC=firma,DC=local):', False);
  LdapPage.Add('Search Base  (OU zawierająca użytkowników):', False);

  LdapPage.Values[0] := 'dc01.agroas.local';
  LdapPage.Values[1] := '636';
  LdapPage.Values[2] := 'DC=AGROAS,DC=local';
  LdapPage.Values[3] := 'OU=Uzytkownicy,OU=AGROAS,DC=AGROAS,DC=local';

  // ── Strona 2: ustawienia usługi ─────────────────────────────────────────
  ServicePage := CreateInputQueryPage(LdapPage.ID,
    'Ustawienia usługi Windows',
    'Port HTTP i uprawnienia administracyjne',
    'Port musi być dostępny w sieci lokalnej. Loginy adminów mogą wysyłać komunikaty do użytkowników.');

  ServicePage.Add('Port HTTP API:', False);
  ServicePage.Add('Loginy administratorów (oddzielone przecinkami):', False);

  ServicePage.Values[0] := '5112';
  ServicePage.Values[1] := GetEnv('USERNAME');
end;

// ── Walidacja przed przejściem dalej ─────────────────────────────────────────
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Port: Integer;
begin
  Result := True;

  if CurPageID = LdapPage.ID then
  begin
    if Trim(LdapPage.Values[0]) = '' then
    begin
      MsgBox('Podaj adres serwera LDAP.', mbError, MB_OK);
      Result := False; Exit;
    end;
    if Trim(LdapPage.Values[2]) = '' then
    begin
      MsgBox('Podaj Base DN, np. DC=firma,DC=local', mbError, MB_OK);
      Result := False; Exit;
    end;
    if Trim(LdapPage.Values[3]) = '' then
    begin
      MsgBox('Podaj Search Base, np. OU=Uzytkownicy,DC=firma,DC=local', mbError, MB_OK);
      Result := False; Exit;
    end;
  end;

  if CurPageID = ServicePage.ID then
  begin
    Port := StrToIntDef(Trim(ServicePage.Values[0]), 0);
    if (Port < 1) or (Port > 65535) then
    begin
      MsgBox('Podaj prawidłowy numer portu (1–65535).', mbError, MB_OK);
      Result := False; Exit;
    end;
    if Trim(ServicePage.Values[1]) = '' then
    begin
      MsgBox('Podaj co najmniej jeden login administratora.', mbError, MB_OK);
      Result := False; Exit;
    end;
  end;
end;

// ── Gettery wartości z kreatora (używane jako {code:...} w [Run]) ────────────
function GetLdapServer(Param: String): String;  begin Result := LdapPage.Values[0];    end;
function GetLdapPort(Param: String): String;    begin Result := LdapPage.Values[1];    end;
function GetBaseDn(Param: String): String;      begin Result := LdapPage.Values[2];    end;
function GetSearchBase(Param: String): String;  begin Result := LdapPage.Values[3];    end;
function GetHttpPort(Param: String): String;    begin Result := ServicePage.Values[0]; end;
function GetAdminLogins(Param: String): String; begin Result := ServicePage.Values[1]; end;
