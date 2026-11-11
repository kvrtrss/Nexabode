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

            if (save) AppSettings.Update(s => s.DarkMode = dark);   // keeps the other saved settings
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