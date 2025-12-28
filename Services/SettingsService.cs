using System.IO;
using System.Text.Json;

namespace PhotoRenamer.Services;

/// <summary>
/// Service for persisting user preferences
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    private UserSettings _currentSettings;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "PhotoRenamer");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
        _currentSettings = Load();
    }

    public UserSettings Settings => _currentSettings;

    public UserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch
        {
            // Ignore errors, return defaults
        }
        return new UserSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public void UpdateLastFolder(string folder)
    {
        _currentSettings.LastFolderPath = folder;
        Save();
    }

    public void UpdateSelectedPattern(string patternId)
    {
        _currentSettings.LastPatternId = patternId;
        Save();
    }

    public void UpdateRecursiveScan(bool recursive)
    {
        _currentSettings.RecursiveScan = recursive;
        Save();
    }
}

/// <summary>
/// User settings data model
/// </summary>
public class UserSettings
{
    public string? LastFolderPath { get; set; }
    public string LastPatternId { get; set; } = "date_time";
    public bool RecursiveScan { get; set; } = true;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
}
