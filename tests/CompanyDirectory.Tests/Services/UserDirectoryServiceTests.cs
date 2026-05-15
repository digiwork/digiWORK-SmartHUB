using CompanyDirectory.Api.Services;
using CompanyDirectory.Infrastructure.Ldap;
using CompanyDirectory.Shared.Configuration;
using CompanyDirectory.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CompanyDirectory.Tests.Services;

public class UserDirectoryServiceTests
{
    private static IOptions<SearchSettings> DefaultSearchOptions(int maxResults = 50)
        => Options.Create(new SearchSettings { MaxResults = maxResults, MinimumQueryLength = 2 });

    private static LdapUserRawEntry MakeRawEntry(string login, string displayName)
        => new()
        {
            DistinguishedName = $"CN={displayName},OU=Users,DC=firma,DC=local",
            Attributes = new(StringComparer.OrdinalIgnoreCase)
            {
                ["sAMAccountName"]     = login,
                ["displayName"]        = displayName,
                ["userAccountControl"] = "512",
            }
        };

    // ── SearchAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_LdapReturnsUsers_ReturnsMappedDtos()
    {
        var ldap = Substitute.For<ILdapService>();
        ldap.SearchUsersAsync("kowalski", Arg.Any<CancellationToken>())
            .Returns(new List<LdapUserRawEntry>
            {
                MakeRawEntry("jan.kowalski",   "Jan Kowalski"),
                MakeRawEntry("maria.kowalczyk","Maria Kowalczyk"),
            });

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.SearchAsync("kowalski");

        Assert.Equal(2,          result.TotalCount);
        Assert.Equal(2,          result.Items.Count);
        Assert.Equal("kowalski", result.Query);
        Assert.Contains(result.Items, u => u.Login == "jan.kowalski");
        Assert.Contains(result.Items, u => u.Login == "maria.kowalczyk");
    }

    [Fact]
    public async Task SearchAsync_LdapReturnsMoreThanMaxResults_TruncatesResults()
    {
        var manyUsers = Enumerable.Range(1, 100)
            .Select(i => MakeRawEntry($"user{i}", $"User {i}"))
            .ToList();

        var ldap = Substitute.For<ILdapService>();
        ldap.SearchUsersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(manyUsers);

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(maxResults: 10), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.SearchAsync("user");

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(10, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_LdapReturnsEmpty_ReturnsEmptyResult()
    {
        var ldap = Substitute.For<ILdapService>();
        ldap.SearchUsersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<LdapUserRawEntry>());

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.SearchAsync("nieistniejacy");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    // ── GetByLoginAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetByLoginAsync_UserNotFound_ReturnsNull()
    {
        var ldap = Substitute.For<ILdapService>();
        ldap.GetUserByLoginAsync("nieistniejacy", Arg.Any<CancellationToken>())
            .Returns((LdapUserRawEntry?)null);

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.GetByLoginAsync("nieistniejacy");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByLoginAsync_UserExists_ReturnsMappedDto()
    {
        var rawEntry = MakeRawEntry("jan.kowalski", "Jan Kowalski");

        var ldap = Substitute.For<ILdapService>();
        ldap.GetUserByLoginAsync("jan.kowalski", Arg.Any<CancellationToken>())
            .Returns(rawEntry);
        // Manager resolution calls SearchUsersAsync — return empty to skip that path
        ldap.SearchUsersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<LdapUserRawEntry>());

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.GetByLoginAsync("jan.kowalski");

        Assert.NotNull(result);
        Assert.Equal("jan.kowalski", result!.Login);
        Assert.Equal("Jan Kowalski", result.DisplayName);
    }

    // ── GetAllUsersAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllActiveLdapUsers()
    {
        var users = new List<LdapUserRawEntry>
        {
            MakeRawEntry("user1", "User One"),
            MakeRawEntry("user2", "User Two"),
            MakeRawEntry("user3", "User Three"),
        };

        var ldap = Substitute.For<ILdapService>();
        ldap.GetAllActiveUsersAsync(Arg.Any<CancellationToken>())
            .Returns(users);

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.GetAllUsersAsync();

        Assert.Equal(3, result.Count);
        Assert.All(result, u => Assert.IsType<UserDirectoryEntryDto>(u));
    }

    // ── SyncAllAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAllAsync_LdapReturnsUsers_ReturnsSuccessResult()
    {
        var users = Enumerable.Range(1, 20).Select(i => MakeRawEntry($"u{i}", $"User {i}")).ToList();

        var ldap = Substitute.For<ILdapService>();
        ldap.GetAllActiveUsersAsync(Arg.Any<CancellationToken>()).Returns(users);

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.SyncAllAsync();

        Assert.Equal(20, result.SyncedCount);
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task SyncAllAsync_LdapThrows_ReturnsFailureResult()
    {
        var ldap = Substitute.For<ILdapService>();
        ldap.GetAllActiveUsersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LDAP server unavailable"));

        var svc = new UserDirectoryService(ldap, DefaultSearchOptions(), NullLogger<UserDirectoryService>.Instance);
        var result = await svc.SyncAllAsync();

        Assert.Equal(0,    result.SyncedCount);
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("LDAP server unavailable", result.Errors[0]);
    }
}
