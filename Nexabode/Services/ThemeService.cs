using Microsoft.Win32;
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
        ["AppSecondary"] = "#617187",
        ["AppPrimary"] = "#4F46E5",
        ["AppBorder"] = "#E2E8F0",

        // Kinds of change in the Change history: a soft fill and a text color for the label, and a stronger bar
        ["CatStayFill"] = "#E0E7FF",
        ["CatStayText"] = "#3730A3",
        ["CatStayBar"] = "#6366F1",
        ["CatBillingFill"] = "#FEF3C7",
        ["CatBillingText"] = "#92400E",
        ["CatBillingBar"] = "#F59E0B",
        ["CatPaymentFill"] = "#DCFCE7",
        ["CatPaymentText"] = "#166534",
        ["CatPaymentBar"] = "#22C55E",
        ["CatAccountFill"] = "#CCFBF1",
        ["CatAccountText"] = "#115E59",
        ["CatAccountBar"] = "#14B8A6",
        ["CatSecurityFill"] = "#FFE4E6",
        ["CatSecurityText"] = "#9F1239",
        ["CatSecurityBar"] = "#F43F5E",
        ["CatOtherFill"] = "#E2E8F0",
        ["CatOtherText"] = "#475569",
        ["CatOtherBar"] = "#94A3B8",
    };

    private static readonly Dictionary<string, string> Dark = new()
    {
        ["AppBackground"] = "#0F172A",
        ["AppSurface"] = "#1E293B",
        ["AppText"] = "#F1F5F9",
        ["AppSecondary"] = "#94A3B8",
        ["AppPrimary"] = "#818CF8",
        ["AppBorder"] = "#334155",

        // Kinds of change in the Change history (dark versions of the same colors)
        ["CatStayFill"] = "#312E81",
        ["CatStayText"] = "#C7D2FE",
        ["CatStayBar"] = "#818CF8",
        ["CatBillingFill"] = "#4A3410",
        ["CatBillingText"] = "#FCD34D",
        ["CatBillingBar"] = "#F59E0B",
        ["CatPaymentFill"] = "#14391F",
        ["CatPaymentText"] = "#86EFAC",
        ["CatPaymentBar"] = "#22C55E",
        ["CatAccountFill"] = "#0F3D3A",
        ["CatAccountText"] = "#5EEAD4",
        ["CatAccountBar"] = "#14B8A6",
        ["CatSecurityFill"] = "#4C1625",
        ["CatSecurityText"] = "#FDA4AF",
        ["CatSecurityBar"] = "#FB7185",
        ["CatOtherFill"] = "#334155",
        ["CatOtherText"] = "#CBD5E1",
        ["CatOtherBar"] = "#64748B",
    };

    // Status colours on a light background. Every one is at least 4.5 to 1 against white and against its own soft fill, so it can be read as text.
    private static readonly Dictionary<string, string> StatusLight = new()
    {
        ["Success"] = "#107535",
        ["Warning"] = "#9A5404",
        ["Danger"] = "#C22121",
        ["Info"] = "#026BA1",
        ["Neutral"] = "#5A687D",
        ["Occupied"] = "#4F46E5",
    };

    // The same colours lightened for the dark background, with the same contrast.
    private static readonly Dictionary<string, string> StatusDark = new()
    {
        ["Success"] = "#4EB975",
        ["Warning"] = "#E1943A",
        ["Danger"] = "#EC8888",
        ["Info"] = "#5DB0DB",
        ["Neutral"] = "#9FA9B7",
        ["Occupied"] = "#A29DF1",
    };

    // What the person picked: "System" (follow Windows), "Light" or "Dark".
    public static string Choice { get; private set; } = "System";

    public static void Initialize()
    {
        var settings = AppSettings.Load();
        // Nobody has chosen yet: follow Windows. An older saved "dark mode" counts as an explicit choice of Dark.
        Choice = settings.ThemeChoice ?? (settings.DarkMode ? "Dark" : "System");
        Apply(WouldBeDark(Choice), save: false);
    }

    // Would this choice look dark right now? "System" asks Windows.
    public static bool WouldBeDark(string choice) => choice switch
    {
        "Dark" => true,
        "Light" => false,
        _ => WindowsPrefersDark()
    };

    // Remembers the choice and applies it. The Windows setting is read when the app starts and when the choice is made.
    public static void SetChoice(string choice)
    {
        Choice = choice is "Light" or "Dark" ? choice : "System";

        // Only switch when the colors really change. Applying the same theme again swaps WPF-UI's styles under the
        // windows that are already open and leaves a blank white screen (for example "Follow Windows" while Windows
        // and the app are both dark). Then only the choice is saved.
        var dark = WouldBeDark(Choice);
        if (dark != IsDark) Apply(dark, save: false);
        AppSettings.Update(s =>
        {
            s.ThemeChoice = Choice;
            s.DarkMode = IsDark;   // kept in step for older settings files
        });
    }

    public static void Toggle() => SetChoice(IsDark ? "Light" : "Dark");

    // Windows keeps "apps use the light theme" as 1 or 0 for the signed-in person.
    private static bool WindowsPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;   // if it can't be read, stay with the light theme
        }
    }

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

            foreach (var (name, hex) in dark ? StatusDark : StatusLight)
            {
                SetBrush(name + "Brush", hex);
                SetBrush(name + "SoftBrush", hex, alpha: 0x26);
            }

            if (save) AppSettings.Update(s => s.DarkMode = dark);   // (not used any more: SetChoice saves)
            ThemeChanged?.Invoke();
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