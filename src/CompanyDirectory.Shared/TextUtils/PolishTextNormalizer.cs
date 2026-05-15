namespace CompanyDirectory.Shared.TextUtils;

/// <summary>
/// Replaces Polish diacritical characters with their ASCII equivalents
/// so that searches work regardless of whether diacritics are typed.
/// </summary>
public static class PolishTextNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace('ą', 'a').Replace('ć', 'c').Replace('ę', 'e')
            .Replace('ł', 'l').Replace('ń', 'n').Replace('ó', 'o')
            .Replace('ś', 's').Replace('ź', 'z').Replace('ż', 'z')
            .Replace('Ą', 'A').Replace('Ć', 'C').Replace('Ę', 'E')
            .Replace('Ł', 'L').Replace('Ń', 'N').Replace('Ó', 'O')
            .Replace('Ś', 'S').Replace('Ź', 'Z').Replace('Ż', 'Z');
    }
}
