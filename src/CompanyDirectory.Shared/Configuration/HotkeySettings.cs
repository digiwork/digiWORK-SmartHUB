namespace CompanyDirectory.Shared.Configuration;

public class HotkeySettings
{
    public const string SectionName = "Hotkey";

    /// <summary>Comma-separated modifiers: Control, Alt, Shift, Win.</summary>
    public string Modifiers { get; set; } = "Control,Alt";

    /// <summary>Virtual key name, e.g. "Space", "F1".</summary>
    public string Key { get; set; } = "Space";
}
