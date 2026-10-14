using System.IO;
using System.Text.Json;
using Nexabode.Data;

namespace Nexabode.Services;

public class AppSettings
{
    public bool DarkMode { get; set; }
    public string HouseName { get; set; } = "";
    public string HouseAddress { get; set; } = "";

    private static string FilePath => Path.Combine(DatabaseKeyProvider.AppFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* a damaged file */ }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DatabaseKeyProvider.AppFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { /* not critical */ }
    }
}