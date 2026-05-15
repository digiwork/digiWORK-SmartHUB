namespace CompanyDirectory.Shared.Configuration;

public class UiSettings
{
    public const string SectionName = "Ui";

    public bool StartMinimizedToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ShowEmployeeCard { get; set; } = false;
    public string Theme { get; set; } = "Light";
}
