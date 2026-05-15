namespace CompanyDirectory.Shared.Configuration;

public class SyncSettings
{
    public const string SectionName = "Sync";

    public bool SyncOnStartup { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}
