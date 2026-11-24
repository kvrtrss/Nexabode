using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nexabode.Controls;

public partial class StatCard : UserControl
{
    public StatCard() => InitializeComponent();

    public string Label
    {
        get => LabelText.Text;
        set => LabelText.Text = value;
    }

    public string Value
    {
        get => ValueText.Text;
        set => ValueText.Text = value;
    }

    // Font size of the big number. 28 by default; pages that repeat the numbers can use a smaller size.
    public double ValueSize
    {
        get => ValueText.FontSize;
        set
        {
            ValueText.FontSize = value;
            ValueText.Margin = new Thickness(0, 3, 0, 0);
        }
    }

    public string Caption
    {
        get => CaptionText.Text;
        set => CaptionText.Text = value;
    }

    // Caption color: Success, Warning, Danger, Info, Neutral, or AppSecondary (default).
    public string Tone
    {
        set
        {
            if (Application.Current.TryFindResource(value + "Brush") is Brush brush)
                CaptionText.Foreground = brush;
        }
    }
}