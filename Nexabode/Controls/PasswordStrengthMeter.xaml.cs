using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Nexabode.Services;

namespace Nexabode.Controls;

public partial class PasswordStrengthMeter : UserControl
{
    public PasswordStrengthMeter() => InitializeComponent();

    // Keeps the meter in step with a password box as the person types.
    // 'username' (optional) lets it warn when the password contains the username.
    public void Attach(Wpf.Ui.Controls.PasswordBox box, Func<string?>? username = null)
    {
        void Refresh() => Update(box.Password, username?.Invoke());

        // Whichever of these the box raises, the meter follows.
        box.AddHandler(System.Windows.Controls.PasswordBox.PasswordChangedEvent,
                       new RoutedEventHandler((_, _) => Refresh()));
        box.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                       new TextChangedEventHandler((_, _) => Refresh()));
    }

    public void Update(string? password, string? username = null)
    {
        var result = PasswordStrengthRules.Evaluate(password, username);
        if (result.Level == PasswordStrength.None)
        {
            Root.Visibility = Visibility.Collapsed;
            return;
        }

        Root.Visibility = Visibility.Visible;

        var color = result.Level switch
        {
            PasswordStrength.Weak => Pick("DangerBrush"),
            PasswordStrength.Fair => Pick("WarningBrush"),
            PasswordStrength.Good => Pick("InfoBrush"),
            _ => Pick("SuccessBrush")
        };
        var off = Pick("AppBorderBrush");

        var filled = (int)result.Level;
        var bars = new[] { Bar1, Bar2, Bar3, Bar4 };
        for (var i = 0; i < bars.Length; i++)
            bars[i].Background = i < filled ? color : off;

        HintText.Inlines.Clear();
        HintText.Inlines.Add(new Run(PasswordStrengthRules.Label(result.Level))
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = color
        });
        HintText.Inlines.Add(new Run("  " + result.Hint) { Foreground = Pick("AppSecondaryBrush") });
    }

    private Brush Pick(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
}
