using System.IO;
using System.Text.Json;
using Nexabode.Data;

namespace Nexabode.Services;

public class AppSettings
{
    public bool DarkMode { get; set; }
    public string HouseName { get; set; } = "";
    public string HouseAddress { get; set; } = "";
    public string LastBackup { get; set; } = "";
    public DateTime? LastBackupAt { get; set; }
    public bool FullRentForPartialMonths { get; set; }   // false = bill partial months by days
    public int ReminderLeadDays { get; set; } = 3;       // how many days ahead notifications warn about due dates
    public List<string> ReadNoticeKeys { get; set; } = new();   // notifications marked as read (a changed situation gets a new key)

    private static string FilePath => Path.Combine(DatabaseKeyProvider.AppFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* a damaged settings file just falls back to defaults */ }
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

    // Changes one setting without touching the others.
    public static void Update(Action<AppSettings> change)
    {
        var settings = Load();
        change(settings);
        settings.Save();
    }

}