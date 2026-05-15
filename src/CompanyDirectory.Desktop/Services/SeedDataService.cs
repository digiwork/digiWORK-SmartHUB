using CompanyDirectory.Shared.Dtos;
using CompanyDirectory_Desktop.LocalCache;
using Microsoft.Extensions.Logging;

namespace CompanyDirectory_Desktop.Services;

public class SeedDataService(IUserCacheRepository repository, ILogger<SeedDataService> logger)
{
    public async Task SeedIfEmptyAsync()
    {
        var count = await repository.GetUserCountAsync();
        if (count > 0) return;

        logger.LogInformation("Database empty — inserting seed data");
        await repository.UpsertUsersAsync(BuildSeedUsers());
        logger.LogInformation("Seed data inserted: {Count} users", SeedUsers.Length);
    }

    private static UserDirectoryEntryDto[] BuildSeedUsers()
    {
        var now = DateTime.UtcNow;
        foreach (var u in SeedUsers)
            u.LastSyncedAt = now;
        return SeedUsers;
    }

    private static readonly UserDirectoryEntryDto[] SeedUsers =
    [
        new() { Login="lukasz.wisniewski",    UserPrincipalName="lukasz.wisniewski@firma.local",    DisplayName="Łukasz Wiśniewski",      FirstName="Łukasz",   LastName="Wiśniewski",    Email="l.wisniewski@firma.pl",   Phone="+48 22 100 0001", Department="IT",          JobTitle="Programista",             Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="zofia.zolkowska",      UserPrincipalName="zofia.zolkowska@firma.local",      DisplayName="Zofia Żółkowska",        FirstName="Zofia",    LastName="Żółkowska",     Email="z.zolkowska@firma.pl",    Phone="+48 22 100 0002", Department="HR",          JobTitle="Specjalista HR",          Company="Firma Sp. z o.o.", Office="Kraków",    IsActive=true },
        new() { Login="anna.sliwinska",       UserPrincipalName="anna.sliwinska@firma.local",       DisplayName="Anna Śliwińska",         FirstName="Anna",     LastName="Śliwińska",     Email="a.sliwinska@firma.pl",    Phone="+48 22 100 0003", Department="Finanse",     JobTitle="Główna Księgowa",         Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="marek.cwikla",         UserPrincipalName="marek.cwikla@firma.local",         DisplayName="Marek Ćwikła",           FirstName="Marek",    LastName="Ćwikła",        Email="m.cwikla@firma.pl",       Phone="+48 22 100 0004", Department="Sprzedaż",    JobTitle="Kierownik Sprzedaży",     Company="Firma Sp. z o.o.", Office="Gdańsk",    IsActive=true },
        new() { Login="piotr.gasiorowski",    UserPrincipalName="piotr.gasiorowski@firma.local",    DisplayName="Piotr Gąsiorowski",      FirstName="Piotr",    LastName="Gąsiorowski",   Email="p.gasiorowski@firma.pl",  Phone="+48 22 100 0005", Department="IT",          JobTitle="Architekt Systemów",      Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="jan.kowalski",         UserPrincipalName="jan.kowalski@firma.local",         DisplayName="Jan Kowalski",           FirstName="Jan",      LastName="Kowalski",      Email="j.kowalski@firma.pl",     Phone="+48 22 100 0006", Department="IT",          JobTitle="Administrator IT",        Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="maria.kowalczyk",      UserPrincipalName="maria.kowalczyk@firma.local",      DisplayName="Maria Kowalczyk",        FirstName="Maria",    LastName="Kowalczyk",     Email="m.kowalczyk@firma.pl",    Phone="+48 22 100 0007", Department="Marketing",   JobTitle="Specjalista ds. Marketingu", Company="Firma Sp. z o.o.", Office="Kraków",  IsActive=true },
        new() { Login="tomasz.nowak",         UserPrincipalName="tomasz.nowak@firma.local",         DisplayName="Tomasz Nowak",           FirstName="Tomasz",   LastName="Nowak",         Email="t.nowak@firma.pl",        Phone="+48 22 100 0008", Department="Zarząd",      JobTitle="Dyrektor Generalny",      Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="katarzyna.wojcik",     UserPrincipalName="katarzyna.wojcik@firma.local",     DisplayName="Katarzyna Wójcik",       FirstName="Katarzyna",LastName="Wójcik",        Email="k.wojcik@firma.pl",       Phone="+48 22 100 0009", Department="Prawny",      JobTitle="Radca Prawny",            Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="adam.lewandowski",     UserPrincipalName="adam.lewandowski@firma.local",     DisplayName="Adam Lewandowski",       FirstName="Adam",     LastName="Lewandowski",   Email="a.lewandowski@firma.pl",  Phone="+48 22 100 0010", Department="IT",          JobTitle="DevOps Engineer",         Company="Firma Sp. z o.o.", Office="Wrocław",   IsActive=true },
        new() { Login="ewa.zielinska",        UserPrincipalName="ewa.zielinska@firma.local",        DisplayName="Ewa Zielińska",          FirstName="Ewa",      LastName="Zielińska",     Email="e.zielinska@firma.pl",    Phone="+48 22 100 0011", Department="HR",          JobTitle="Rekruter",                Company="Firma Sp. z o.o.", Office="Kraków",    IsActive=true },
        new() { Login="robert.wieczorek",     UserPrincipalName="robert.wieczorek@firma.local",     DisplayName="Robert Wieczorek",       FirstName="Robert",   LastName="Wieczorek",     Email="r.wieczorek@firma.pl",    Phone="+48 22 100 0012", Department="Sprzedaż",    JobTitle="Handlowiec",              Company="Firma Sp. z o.o.", Office="Poznań",    IsActive=true },
        new() { Login="agnieszka.szymanska",  UserPrincipalName="agnieszka.szymanska@firma.local",  DisplayName="Agnieszka Szymańska",    FirstName="Agnieszka",LastName="Szymańska",     Email="a.szymanska@firma.pl",    Phone="+48 22 100 0013", Department="Finanse",     JobTitle="Analityk Finansowy",      Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="michal.dabrowski",     UserPrincipalName="michal.dabrowski@firma.local",     DisplayName="Michał Dąbrowski",       FirstName="Michał",   LastName="Dąbrowski",     Email="m.dabrowski@firma.pl",    Phone="+48 22 100 0014", Department="IT",          JobTitle="Tester Oprogramowania",   Company="Firma Sp. z o.o.", Office="Wrocław",   IsActive=true },
        new() { Login="barbara.kaminska",     UserPrincipalName="barbara.kaminska@firma.local",     DisplayName="Barbara Kamińska",       FirstName="Barbara",  LastName="Kamińska",      Email="b.kaminska@firma.pl",     Phone="+48 22 100 0015", Department="Administracja",JobTitle="Asystent Zarządu",        Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="grzegorz.zajac",       UserPrincipalName="grzegorz.zajac@firma.local",       DisplayName="Grzegorz Zając",         FirstName="Grzegorz", LastName="Zając",         Email="g.zajac@firma.pl",        Phone="+48 22 100 0016", Department="IT",          JobTitle="Programista Senior",      Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true, ManagerDisplayName="Jan Kowalski" },
        new() { Login="malgorzata.krawczyk",  UserPrincipalName="malgorzata.krawczyk@firma.local",  DisplayName="Małgorzata Krawczyk",    FirstName="Małgorzata",LastName="Krawczyk",     Email="m.krawczyk@firma.pl",     Phone="+48 22 100 0017", Department="Marketing",   JobTitle="Copywriter",              Company="Firma Sp. z o.o.", Office="Gdańsk",    IsActive=true },
        new() { Login="pawel.mazur",          UserPrincipalName="pawel.mazur@firma.local",          DisplayName="Paweł Mazur",            FirstName="Paweł",    LastName="Mazur",         Email="p.mazur@firma.pl",        Phone="+48 22 100 0018", Department="Sprzedaż",    JobTitle="Key Account Manager",     Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
        new() { Login="joanna.piotrowska",    UserPrincipalName="joanna.piotrowska@firma.local",    DisplayName="Joanna Piotrowska",      FirstName="Joanna",   LastName="Piotrowska",    Email="j.piotrowska@firma.pl",   Phone="+48 22 100 0019", Department="IT",          JobTitle="UX Designer",             Company="Firma Sp. z o.o.", Office="Kraków",    IsActive=true },
        new() { Login="krzysztof.jankowski",  UserPrincipalName="krzysztof.jankowski@firma.local",  DisplayName="Krzysztof Jankowski",    FirstName="Krzysztof",LastName="Jankowski",     Email="k.jankowski@firma.pl",    Phone="+48 22 100 0020", Department="IT",          JobTitle="Security Engineer",       Company="Firma Sp. z o.o.", Office="Warszawa",  IsActive=true },
    ];
}
