using System.Globalization;

namespace Nexabode.Services;

// Works out how old the last backup is, so the app can remind the admin to make a new one.
public static class BackupReminder
{
    public const int DaysBeforeReminder = 7;

    private const string OldFormat = "MMM d, yyyy h:mm tt";   // how version 1.0.x wrote the date

    public static DateTime? LastBackupTime()
    {
        var settings = AppSettings.Load();
        if (settings.LastBackupAt is DateTime at) return at;

        // Settings saved by version 1.0.x only have the text version of the date.
        if (DateTime.TryParseExact(settings.LastBackup, OldFormat, CultureInfo.CurrentCulture,
                                   DateTimeStyles.None, out var old))
            return old;

        return null;
    }

    public static int? DaysSinceBackup() =>
        LastBackupTime() is DateTime last ? (int?)Math.Max(0, (DateTime.Today - last.Date).Days) : null;

    // True when there has never been a backup, or the last one is 7 or more days old.
    public static bool IsDue => DaysSinceBackup() is not int days || days >= DaysBeforeReminder;
}