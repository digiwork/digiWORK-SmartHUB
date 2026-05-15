namespace CompanyDirectory.Shared.Errors;

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }

    public static ApiError From(string code, string message, string? details = null)
        => new() { Code = code, Message = message, Details = details };
}
