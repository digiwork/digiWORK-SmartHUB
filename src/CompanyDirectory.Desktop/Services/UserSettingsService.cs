using System.Text.Json;
using System.Text.Json.Nodes;

namespace CompanyDirectory_Desktop.Services;

public sealed class UserSettingsService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _path;

    public UserSettingsService()
    {
        var dir = Path.Combine(
            Environment.ExpandEnvironmentVariables("%LocalAppData%"),
            "CompanyDirectory");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "usersettings.json");
    }

    public void Save(
        bool   startMinimizedToTray,
        bool   closeToTray,
        bool   showEmployeeCard,
        string hotkeyModifiers,
        string hotkeyKey,
        int    syncIntervalMinutes,
        string apiBaseUrl)
    {
        var json = ReadJson();
        var ui   = json["Ui"] as JsonObject ?? new JsonObject();
        ui["StartMinimizedToTray"] = startMinimizedToTray;
        ui["CloseToTray"]          = closeToTray;
        ui["ShowEmployeeCard"]     = showEmployeeCard;
        json["Ui"] = ui;
        json["Hotkey"] = new JsonObject
        {
            ["Modifiers"] = hotkeyModifiers,
            ["Key"]       = hotkeyKey,
        };
        json["Sync"] = new JsonObject
        {
            ["IntervalMinutes"] = syncIntervalMinutes,
        };
        json["Api"] = new JsonObject
        {
            ["BaseUrl"] = apiBaseUrl,
        };
        WriteJson(json);
    }

    public void SaveTheme(bool isDarkMode)
    {
        var json = ReadJson();
        var ui   = json["Ui"] as JsonObject ?? new JsonObject();
        ui["Theme"]  = isDarkMode ? "Dark" : "Light";
        json["Ui"]   = ui;
        WriteJson(json);
    }

    public void SaveWindowBounds(string key, int logicalWidth, int logicalHeight, int logicalX, int logicalY)
    {
        var json    = ReadJson();
        var windows = json["Windows"] as JsonObject ?? new JsonObject();
        windows[key] = new JsonObject
        {
            ["Width"]  = logicalWidth,
            ["Height"] = logicalHeight,
            ["X"]      = logicalX,
            ["Y"]      = logicalY,
        };
        json["Windows"] = windows;
        WriteJson(json);
    }

    public (int Width, int Height, int X, int Y)? LoadWindowBounds(string key)
    {
        var win = ReadJson()["Windows"]?[key];
        if (win is null) return null;
        return (
            win["Width"]?.GetValue<int>()  ?? -1,
            win["Height"]?.GetValue<int>() ?? -1,
            win["X"]?.GetValue<int>()      ?? -1,
            win["Y"]?.GetValue<int>()      ?? -1
        );
    }

    public string FilePath => _path;

    private JsonObject ReadJson()
    {
        if (!File.Exists(_path)) return new JsonObject();
        try   { return JsonNode.Parse(File.ReadAllText(_path)) as JsonObject ?? new JsonObject(); }
        catch { return new JsonObject(); }
    }

    private void WriteJson(JsonObject json)
        => File.WriteAllText(_path, json.ToJsonString(WriteOptions));
}
