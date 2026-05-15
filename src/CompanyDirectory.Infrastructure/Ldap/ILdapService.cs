namespace CompanyDirectory.Infrastructure.Ldap;

public interface ILdapService
{
    Task<List<LdapUserRawEntry>> SearchUsersAsync(string query, CancellationToken ct = default);
    Task<LdapUserRawEntry?> GetUserByLoginAsync(string login, CancellationToken ct = default);
    Task<List<LdapUserRawEntry>> GetAllActiveUsersAsync(CancellationToken ct = default);
    Task<List<string>> GetUserGroupsAsync(string distinguishedName, CancellationToken ct = default);
}
