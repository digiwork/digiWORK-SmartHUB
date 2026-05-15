using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace CompanyDirectory_Desktop.Services;

public sealed class AutostartService(ILogger<AutostartService> logger)
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName    = "CompanyDirectory";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) is not null;
        }
    }

    public void SetEnabled(bool value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            logger.LogWarning("Cannot open registry Run key");
            return;
        }

        if (value)
        {
            var exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            key.SetValue(AppName, $"\"{exe}\"");
            logger.LogInformation("Autostart enabled");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            logger.LogInformation("Autostart disabled");
        }
    }
}
