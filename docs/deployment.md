# Instrukcja wdrożenia — CompanyDirectory

## Spis treści

1. [Wymagania wstępne](#1-wymagania-wstępne)
2. [Konto usługi Active Directory](#2-konto-usługi-active-directory)
3. [Wdrożenie API](#3-wdrożenie-api)
4. [Wdrożenie klienta Desktop](#4-wdrożenie-klienta-desktop)
5. [Kompletny opis konfiguracji](#5-kompletny-opis-konfiguracji)
6. [Logi i diagnostyka](#6-logi-i-diagnostyka)
7. [Rozwiązywanie problemów](#7-rozwiązywanie-problemów)

---

## 1. Wymagania wstępne

### Serwer API

| Element | Minimalna wersja |
|---------|-----------------|
| Windows Server | 2019 lub nowszy |
| .NET Runtime | 10.0 (ASP.NET Core) |
| IIS | 10.0 + ASP.NET Core Hosting Bundle |
| Dostęp sieciowy | LDAPS do kontrolera domeny (TCP 636) |

### Stacja robocza użytkownika (klient Desktop)

| Element | Minimalna wersja |
|---------|-----------------|
| Windows | 10 1903 (build 18362) lub Windows 11 |
| .NET Desktop Runtime | 10.0 |
| Windows App SDK Runtime | 2.0.1 |
| Dostęp sieciowy | HTTPS do serwera API |

---

## 2. Konto usługi Active Directory

API łączy się z LDAP w trybie `UseDefaultCredentials=true` — czyli pod tożsamością puli aplikacyjnej IIS (lub konta uruchamiającego usługę).

### Minimalne uprawnienia konta usługi

```
CN=svc-companydirectory,OU=ServiceAccounts,DC=firma,DC=local
```

| Uprawnienie | Zakres | Cel |
|-------------|--------|-----|
| Read | `OU=Users,DC=firma,DC=local` | Odczyt atrybutów użytkowników |
| List Contents | `OU=Groups,DC=firma,DC=local` | Odczyt członkostwa w grupach |

> Konto **nie potrzebuje** uprawnień administracyjnych. Wystarczy domyślna rola "Domain Users" z dodatkowym uprawnieniem Read na właściwym OU.

### Włączenie LDAPS (port 636)

1. Na kontrolerze domeny zainstaluj certyfikat SSL (urząd certyfikacji domeny lub zewnętrzny).
2. Zweryfikuj dostępność:
   ```powershell
   Test-NetConnection dc01.firma.local -Port 636
   ```

---

## 3. Wdrożenie API

### 3a. Budowanie paczki publikacyjnej

```powershell
dotnet publish src/CompanyDirectory.Api/CompanyDirectory.Api.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output C:\deploy\CompanyDirectory.Api
```

### 3b. Konfiguracja IIS

1. Zainstaluj **ASP.NET Core Hosting Bundle** na serwerze.
2. Utwórz nową pulę aplikacyjną:
   - Nazwa: `CompanyDirectory`
   - Wersja .NET CLR: `No Managed Code`
   - Tożsamość: konto usługi AD z punktu 2
3. Utwórz witrynę/aplikację IIS wskazującą na `C:\deploy\CompanyDirectory.Api`.
4. Edytuj `appsettings.json` w katalogu publikacji (patrz sekcja 5).

### 3c. Weryfikacja po wdrożeniu

```powershell
Invoke-RestMethod https://companydirectory-api.firma.local/api/health
# Oczekiwany wynik: { "status": "ok", "version": "10.0.0", ... }
```

### 3d. Alternatywa — Windows Service

```powershell
sc.exe create CompanyDirectoryApi `
    binPath="dotnet C:\deploy\CompanyDirectory.Api\CompanyDirectory.Api.dll" `
    start=auto obj="FIRMA\svc-companydirectory" password="..."

sc.exe start CompanyDirectoryApi
```

---

## 4. Wdrożenie klienta Desktop

### 4a. Budowanie pakietu MSIX

W Visual Studio:
1. Kliknij prawym przyciskiem na projekt `CompanyDirectory.Desktop`
2. **Publish → Create App Packages → Sideloading**
3. Wybierz platformę `x64` (lub `x86` dla 32-bit)
4. Podpisz certyfikatem (samopodpisanym lub firmowym)

Z wiersza poleceń:
```powershell
dotnet build src/CompanyDirectory.Desktop/CompanyDirectory.Desktop.csproj `
    --configuration Release `
    -p:Platform=x64 `
    -p:AppxPackageDir="C:\deploy\Desktop\" `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxBundle=Never
```

### 4b. Instalacja na stacjach roboczych

**Wymagania wstępne** (jednorazowo na stacji):
```powershell
# Zainstaluj Windows App SDK Runtime
winget install Microsoft.WindowsAppRuntime.2.0

# Zainstaluj .NET Desktop Runtime 10
winget install Microsoft.DotNet.DesktopRuntime.10
```

**Instalacja pakietu MSIX:**
```powershell
Add-AppxPackage -Path "C:\deploy\Desktop\CompanyDirectory.Desktop_1.0.0_x64.msix"
```

**Wdrożenie grupowe (GPO / Intune):** użyj skryptu powyżej jako skryptu startowego lub pakietu Intune (`.intunewin`).

### 4c. Konfiguracja Desktop po instalacji

Ustawienia użytkownika zapisywane są w:
```
%LocalAppData%\CompanyDirectory\usersettings.json
```

Przy pierwszym uruchomieniu aplikacja:
1. Tworzy bazę SQLite w `%LocalAppData%\CompanyDirectory\directory.db`
2. Wstawia dane testowe (20 pracowników) jeśli baza jest pusta
3. Po 5 sekundach próbuje zsynchronizować dane z API

> **Uwaga:** jeśli API jest niedostępne, aplikacja działa w trybie offline z danymi z cache (lub danymi testowymi przy pierwszym uruchomieniu).

---

## 5. Kompletny opis konfiguracji

### API — `appsettings.json`

```jsonc
{
  "Ldap": {
    "Server": "dc01.firma.local",   // FQDN lub IP kontrolera domeny
    "Port": 636,                    // 636 = LDAPS, 389 = LDAP (niezaszyfrowane)
    "UseSsl": true,                 // true = LDAPS (zalecane)
    "BaseDn": "DC=firma,DC=local",  // korzeń drzewa AD
    "SearchBase": "OU=Users,DC=firma,DC=local",  // OU z kontami użytkowników
    "UseDefaultCredentials": true,  // tożsamość puli aplikacyjnej IIS
    "ServiceAccountUser": "",       // alternatywnie: explicit credentials
    "ServiceAccountPassword": "",
    "PageSize": 500                 // liczba rekordów LDAP na stronę (max 1000)
  },
  "Search": {
    "MinimumQueryLength": 2,        // min. długość zapytania w /api/users/search
    "MaxResults": 50                // maks. wyników wyszukiwania
  },
  "Authentication": {
    "RequireWindowsAuth": true,
    "AdminGroup": "CN=IT-Admins,OU=Groups,DC=firma,DC=local"  // grupa dla POST /sync
  }
}
```

### Desktop — `appsettings.json`

```jsonc
{
  "Api": {
    "BaseUrl": "https://companydirectory-api.firma.local"  // URL do API
  },
  "LocalCache": {
    "DatabasePath": "%LocalAppData%\\CompanyDirectory\\directory.db"
  },
  "Hotkey": {
    "Modifiers": "Control,Alt",  // Control | Alt | Shift | Win (przecinek = AND)
    "Key": "Space"               // nazwa z System.Windows.Input.Key
  },
  "Sync": {
    "SyncOnStartup": true,       // synchronizuj z API po uruchomieniu (5s opóźnienie)
    "IntervalMinutes": 60        // interwał synchronizacji w tle
  },
  "Ui": {
    "StartMinimizedToTray": false,  // ukryj okno przy starcie
    "CloseToTray": true             // przycisk X chowa zamiast zamykać
  },
  "Search": {
    "MinimumQueryLength": 2,
    "MaxResults": 50
  }
}
```

### Ustawienia użytkownika (opcjonalne nadpisanie)

Plik `%LocalAppData%\CompanyDirectory\usersettings.json` nadpisuje wartości z `appsettings.json`. Jest tworzony przez okno Ustawień w aplikacji:

```jsonc
{
  "Ui":     { "StartMinimizedToTray": true, "CloseToTray": true },
  "Hotkey": { "Modifiers": "Control,Alt", "Key": "F1" },
  "Sync":   { "IntervalMinutes": 30 },
  "Api":    { "BaseUrl": "https://inny-serwer.firma.local" }
}
```

---

## 6. Logi i diagnostyka

### API

Logi zapisywane przez Serilog do katalogu aplikacji (`log-.txt`, rotacja dzienna, 30 dni):

```
C:\deploy\CompanyDirectory.Api\log-20260508.txt
```

Poziom logowania zmień w `appsettings.json`:
```json
"Serilog": { "MinimumLevel": { "Default": "Debug" } }
```

### Desktop

Logi w profilu użytkownika:
```
%LocalAppData%\CompanyDirectory\Logs\log-20260508.txt
```

---

## 7. Rozwiązywanie problemów

### API nie odpowiada na `/api/health`

1. Sprawdź stan puli aplikacyjnej IIS (`iisreset` / Event Viewer).
2. Sprawdź port i certyfikat HTTPS.
3. Sprawdź logi aplikacji pod kątem wyjątków startowych.

### Błąd LDAP: `Server is unavailable`

1. Zweryfikuj łączność: `Test-NetConnection dc01.firma.local -Port 636`
2. Sprawdź ważność certyfikatu SSL na DC.
3. Jeśli certyfikat jest samopodpisany i nie jest w zaufanym magazynie, ustaw `"UseSsl": false` i `"Port": 389` (tylko środowisko testowe!).

### Desktop: `Skrót klawiszowy niedostępny` (toast)

Skrót `Ctrl+Alt+Space` jest zajęty przez inną aplikację. Zmień go w menu tray → **Ustawienia** → pole *Modyfikatory* / *Klawisz*.

### Desktop: wyszukiwanie nie zwraca polskich imion

Upewnij się, że kolumna `SearchText` w bazie danych jest zapełniona. Usuń plik bazy (`directory.db`) i uruchom aplikację ponownie, aby wymusić nowe seedowanie i synchronizację.

### Desktop: dane nieaktualne po synchronizacji z AD

1. Sprawdź logi Desktop — szukaj `Cache sync completed` lub `API unreachable`.
2. Zweryfikuj, że konto użytkownika ma dostęp do API (Windows Auth / Kerberos).
3. Sprawdź, czy URL API jest poprawny w Ustawieniach.

### MSIX: błąd instalacji `0x80073CF0`

Certyfikat podpisujący pakiet nie jest zaufany na stacji. Dodaj go do magazynu **Trusted People** lub podpisz certyfikatem CA organizacji.
