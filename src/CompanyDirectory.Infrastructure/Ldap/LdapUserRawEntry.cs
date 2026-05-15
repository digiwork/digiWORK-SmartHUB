namespace CompanyDirectory.Infrastructure.Ldap;

/// <summary>Raw attributes returned from a single LDAP search entry.</summary>
public class LdapUserRawEntry
{
    public string DistinguishedName { get; set; } = string.Empty;

    /// <summary>Keyed by attribute name (lowercase). Values are string or byte[] for binary attrs.</summary>
    public Dictionary<string, object?> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? GetString(string attribute)
    {
        if (!Attributes.TryGetValue(attribute, out var value))
            return null;

        return value switch
        {
            string s => s,
            byte[] b => System.Text.Encoding.UTF8.GetString(b),
            _ => value?.ToString()
        };
    }

    public byte[]? GetBytes(string attribute)
    {
        if (!Attributes.TryGetValue(attribute, out var value))
            return null;

        return value as byte[];
    }

    public string[] GetStringArray(string attribute)
    {
        if (!Attributes.TryGetValue(attribute, out var value))
            return [];

        return value switch
        {
            string[] arr => arr,
            string s => [s],
            _ => []
        };
    }
}
