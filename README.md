# CompanyDirectory

Aplikacja do wyszukiwania pracowników w Active Directory, składająca się z:

- **CompanyDirectory.Api** — REST API (ASP.NET Core 10) z autoryzacją Windows  
- **CompanyDirectory.Desktop** — klient WinUI 3 z ikoną w tray i globalnym skrótem klawiszowym  
- **CompanyDirectory.Shared** — wspólne DTO i konfiguracja  
- **CompanyDirectory.Infrastructure** — warstwa LDAP  
- **CompanyDirectory.Tests** — testy jednostkowe (xUnit + NSubstitute)

## Wymagania minimalne

| Komponent | Wymaganie |
|-----------|-----------|
| System    | Windows 10 1903+ (build 18362) lub Windows 11 |
| .NET      | .NET 10 SDK (`dotnet --version` ≥ 10.0) |
| Serwer AD | Windows Server 2016+ z włączonym LDAPS (port 636) |

## Szybki start (środowisko deweloperskie)

```powershell
# 1. Klonuj / otwórz projekt
cd "D:\Projekt Visual Studio\CompanyDirectory"

# 2. Zbuduj całe rozwiązanie
dotnet build src/CompanyDirectory.Api/CompanyDirectory.Api.csproj
dotnet build src/CompanyDirectory.Desktop/CompanyDirectory.Desktop.csproj -p:Platform=x86

# 3. Uruchom testy
dotnet test tests/CompanyDirectory.Tests/

# 4. Uruchom API
dotnet run --project src/CompanyDirectory.Api

# 5. Aplikacja Desktop — uruchom z Visual Studio (F5) lub
#    otwórz MSIX z bin\x86\Debug\AppPackages\
```

> Pełna instrukcja wdrożenia produkcyjnego: [`docs/deployment.md`](docs/deployment.md)

## Struktura katalogów

```
CompanyDirectory/
├── src/
│   ├── CompanyDirectory.Api/          # ASP.NET Core REST API
│   ├── CompanyDirectory.Desktop/      # WinUI 3 tray client
│   ├── CompanyDirectory.Infrastructure/  # LDAP service
│   └── CompanyDirectory.Shared/       # DTOs, config, TextUtils
├── tests/
│   └── CompanyDirectory.Tests/        # Unit + integration tests
└── docs/
    └── deployment.md                  # Instrukcja wdrożenia
```

## Konfiguracja (skrót)

Przed pierwszym uruchomieniem edytuj:

- `src/CompanyDirectory.Api/appsettings.json` → sekcja `Ldap` (serwer DC, BaseDN)
- `src/CompanyDirectory.Desktop/appsettings.json` → sekcja `Api.BaseUrl`

Szczegółowy opis wszystkich parametrów: [`docs/deployment.md`](docs/deployment.md)
