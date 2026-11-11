using System.Windows;
using System.Windows.Controls;
using Nexabode.Services;

namespace Nexabode.Views;

// "Review changes": shows what a change would affect before it is applied. The report is plain text with a few
// line markers (see ImpactWording); this window turns them into headings, a summary box, bullets and notes.
public partial class ImpactDialog : Window
{
    private ImpactDialog(string tenantName, string report, string applyText)
    {
        InitializeComponent();
        Title = $"Review changes - {tenantName}";
        TitleText.Text = Title;
        ApplyButton.Content = applyText;
        Draw(report);
    }

    // True if the administrator chose to apply the change.
    public static bool Confirm(Window? owner, string tenantName, string report, string applyText = "Save changes")
    {
        var dialog = new ImpactDialog(tenantName, report, applyText);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private object Res(string key) => FindResource(key);

    private void Draw(string report)
    {
        var first = true;
        foreach (var raw in report.Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            if (raw.StartsWith(ImpactWording.Heading))
            {
                ReportPanel.Children.Add(new TextBlock
                {
                    Text = raw[ImpactWording.Heading.Length..],
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (System.Windows.Media.Brush)Res("AppTextBrush"),
                    Margin = new Thickness(0, first ? 0 : 18, 0, 6)
                });
            }
            else if (raw.StartsWith(ImpactWording.SubHeading))
            {
                ReportPanel.Children.Add(new TextBlock
                {
                    Text = raw[ImpactWording.SubHeading.Length..],
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (System.Windows.Media.Brush)Res("AppSecondaryBrush"),
                    Margin = new Thickness(0, 10, 0, 4)
                });
            }
            else if (raw.StartsWith(ImpactWording.Summary))
            {
                ReportPanel.Children.Add(new Border
                {
                    Style = (Style)Res("BannerInfo"),
                    Margin = new Thickness(0, 0, 0, 6),
                    Child = new TextBlock
                    {
                        Text = raw[ImpactWording.Summary.Length..],
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (System.Windows.Media.Brush)Res("AppTextBrush")
                    }
                });
            }
            else if (raw.StartsWith(ImpactWording.Note))
            {
                ReportPanel.Children.Add(new TextBlock
                {
                    Text = raw[ImpactWording.Note.Length..],
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)Res("AppSecondaryBrush"),
                    Margin = new Thickness(0, first ? 0 : 6, 0, 0)
                });
            }
            else if (raw.StartsWith("  - "))
            {
                ReportPanel.Children.Add(Bullet(raw[4..]));
            }
            else
            {
                // An indented detail line (a utility share), or any other plain line.
                ReportPanel.Children.Add(new TextBlock
                {
                    Text = raw.Trim(),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (System.Windows.Media.Brush)Res(raw.StartsWith("   ") ? "AppSecondaryBrush" : "AppTextBrush"),
                    Margin = new Thickness(raw.StartsWith("   ") ? 22 : 0, 0, 0, 4)
                });
            }

            first = false;
        }
    }

    // A bullet with a hanging indent, so wrapped lines line up under the text and not under the dot.
    private Grid Bullet(string text)
    {
        var needsDecision = text.Contains("NEEDS YOUR DECISION");
        var grid = new Grid { Margin = new Thickness(2, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new TextBlock { Text = "\u2022", Foreground = (System.Windows.Media.Brush)Res("AppSecondaryBrush") };
        var body = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)Res(needsDecision ? "DangerBrush" : "AppTextBrush")
        };
        Grid.SetColumn(body, 1);
        grid.Children.Add(dot);
        grid.Children.Add(body);
        return grid;
    }
}
