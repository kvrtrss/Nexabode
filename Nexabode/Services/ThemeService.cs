using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Nexabode.Services;

public static class ThemeService
{
    public static bool IsDark { get; private set; }
    public static event Action? ThemeChanged;

    private static readonly Dictionary<string, string> Light = new()
    {
        ["AppBackground"] = "#F5F7FB",
        ["AppSurface"] = "#FFFFFF",
        ["AppText"] = "#1E293B",
        ["AppSecondary"] = "#64748B",
        ["AppPrimary"] = "#4F46E5",
        ["AppBorder"] = "#E2E8F0",
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["AppBackground"] = "#0F172A",
        ["AppSurface"] = "#1E293B",
        ["AppText"] = "#F1F5F9",
        ["AppSecondary"] = "#94A3B8",
        ["AppPrimary"] = "#818CF8",
        ["AppBorder"] = "#334155",
    };

    private static readonly Dictionary<string, string> Status = new()
    {
        ["Success"] = "#16A34A",   // Paid, Available
        ["Warning"] = "#D97706",   // Pending, Reserved
        ["Danger"] = "#DC2626",   // Overdue, Maintenance
        ["Info"] = "#0284C7",   // Information
        ["Neutral"] = "#64748B",   // Inactive
        ["Occupied"] = "#4F46E5",  // Occupied beds
    };

    public static void Initialize() => Apply(AppSettings.Load().DarkMode, save: false);

    public static void Toggle() => Apply(!IsDark, save: true);

    public static void Apply(bool dark, bool save)
    {
        try
        {
            IsDark = dark;
            var theme = dark ? ApplicationTheme.Dark : ApplicationTheme.Light;

            // Let WPF-UI switch its own control styles (the one supported way).
            ApplicationThemeManager.Apply(theme, WindowBackdropType.None, false);

            // Then update our own colors.
            var palette = dark ? Dark : Light;
            foreach (var (name, hex) in palette)
                SetBrush(name + "Brush", hex);

            foreach (var (name, hex) in Status)
            {
                SetBrush(name + "Brush", hex);
                SetBrush(name + "SoftBrush", hex, alpha: 0x26);
            }

            if (save) AppSettings.Update(s => s.DarkMode = dark);
            ThemeChanged?.Invoke();
            if (save)
            {
                var settings = AppSettings.Load();   // keep the other saved settings
                settings.DarkMode = dark;
                settings.Save();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Theme error:\n" + ex.Message, "Nexabode",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static void SetBrush(string key, string hex, byte alpha = 0xFF)
    {
        var color = Parse(hex);
        color.A = alpha;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }
}