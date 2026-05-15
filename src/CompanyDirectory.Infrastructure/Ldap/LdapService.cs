using CompanyDirectory.Shared.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using System.Net;

namespace CompanyDirectory.Infrastructure.Ldap;

public class LdapService(IOptions<LdapSettings> options, ILogger<LdapService> logger) : ILdapService
{
    private static readonly string ActiveUsersFilter =
        "(&(objectClass=user)(objectCategory=person)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";

    private readonly LdapSettings _settings = options.Value;

    public async Task<List<LdapUserRawEntry>> SearchUsersAsync(string query, CancellationToken ct = default)
    {
        var sanitized = SanitizeLdapQuery(query);
        var filter = $"(&(objectClass=user)(objectCategory=person)(!(userAccountControl:1.2.840.113556.1.4.803:=2))" +
                     $"(|(sAMAccountName=*{sanitized}*)(displayName=*{sanitized}*)(mail=*{sanitized}*)" +
                     $"(givenName=*{sanitized}*)(sn=*{sanitized}*)))";

        return await ExecuteSearchAsync(filter, ct);
    }

    public async Task<LdapUserRawEntry?> GetUserByLoginAsync(string login, CancellationToken ct = default)
    {
        var sanitized = SanitizeLdapQuery(login);
        var filter = $"(&(objectClass=user)(objectCategory=person)(sAMAccountName={sanitized}))";
        var results = await ExecuteSearchAsync(filter, ct);
        return results.FirstOrDefault();
    }

    public Task<List<LdapUserRawEntry>> GetAllActiveUsersAsync(CancellationToken ct = default)
        => ExecuteSearchAsync(ActiveUsersFilter, ct);

    public async Task<List<string>> GetUserGroupsAsync(string distinguishedName, CancellationToken ct = default)
    {
        var sanitized = SanitizeLdapQuery(distinguishedName);
        var filter = $"(&(objectClass=user)(distinguishedName={sanitized}))";
        var results = await ExecuteSearchAsync(filter, ct, ["memberOf"]);

        if (results.Count == 0) return [];
        return [.. results[0].GetStringArray("memberOf")];
    }

    private Task<List<LdapUserRawEntry>> ExecuteSearchAsync(
        string filter, CancellationToken ct, string[]? attributeOverride = null)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<LdapUserRawEntry>();

            try
            {
                using var connection = CreateConnection();
                var attributes = attributeOverride ?? [.. _settings.AllowedAttributes];
                byte[]? pageCookie = null;

                do
                {
                    ct.ThrowIfCancellationRequested();

                    var request = new SearchRequest(
                        _settings.SearchBase,
                        filter,
                        SearchScope.Subtree,
                        attributes);

                    var pageControl = new PageResultRequestControl(_settings.PageSize);
                    if (pageCookie != null)
                        pageControl.Cookie = pageCookie;
                    request.Controls.Add(pageControl);

                    var response = (SearchResponse)connection.SendRequest(request);

                    foreach (SearchResultEntry entry in response.Entries)
                        results.Add(MapEntry(entry));

                    // Advance paging cookie
                    pageCookie = null;
                    foreach (DirectoryControl ctrl in response.Controls)
                    {
                        if (ctrl is PageResultResponseControl prc && prc.Cookie.Length > 0)
                        {
                            pageCookie = prc.Cookie;
                            break;
                        }
                    }

                } while (pageCookie != null);

                sw.Stop();
                logger.LogInformation("LDAP search completed in {ElapsedMs}ms, returned {Count} entries",
                    sw.ElapsedMilliseconds, results.Count);
            }
            catch (LdapException ex)
            {
                logger.LogError(ex, "LDAP error (code {ErrorCode}) during search", ex.ErrorCode);
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error during LDAP search");
                throw;
            }

            return results;
        }, ct);
    }

    private LdapConnection CreateConnection()
    {
        var identifier = new LdapDirectoryIdentifier(_settings.Server, _settings.Port);

        NetworkCredential? credential = null;
        if (!_settings.UseDefaultCredentials &&
            !string.IsNullOrEmpty(_settings.ServiceAccountUser))
        {
            credential = new NetworkCredential(
                _settings.ServiceAccountUser,
                _settings.ServiceAccountPassword);
        }

        var connection = credential != null
            ? new LdapConnection(identifier, credential)
            : new LdapConnection(identifier);

        connection.SessionOptions.ProtocolVersion = 3;
        connection.AuthType = _settings.UseDefaultCredentials
            ? AuthType.Negotiate
            : AuthType.Basic;

        if (_settings.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
            // Accept server certificate — in production, validate against trusted CA
            connection.SessionOptions.VerifyServerCertificate = (conn, cert) => true;
        }

        connection.Bind();
        return connection;
    }

    private LdapUserRawEntry MapEntry(SearchResultEntry entry)
    {
        var raw = new LdapUserRawEntry { DistinguishedName = entry.DistinguishedName };

        foreach (DirectoryAttribute attr in entry.Attributes.Values)
        {
            if (attr.Count == 0) continue;

            var name = attr.Name;

            // Binary attributes stored as byte[]
            if (name.Equals("thumbnailPhoto", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("objectSid",      StringComparison.OrdinalIgnoreCase))
            {
                raw.Attributes[name] = attr[0] as byte[];
                continue;
            }

            // Multi-value attributes stored as string[]
            if (name.Equals("memberOf", StringComparison.OrdinalIgnoreCase))
            {
                var values = new string[attr.Count];
                for (int i = 0; i < attr.Count; i++)
                    values[i] = attr[i]?.ToString() ?? string.Empty;
                raw.Attributes[name] = values;
                continue;
            }

            // Single-value string
            raw.Attributes[name] = attr[0]?.ToString() ?? string.Empty;
        }

        return raw;
    }

    /// <summary>Escapes LDAP special characters to prevent injection.</summary>
    private static string SanitizeLdapQuery(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }
}
