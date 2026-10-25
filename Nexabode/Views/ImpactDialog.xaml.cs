using System.Windows;

namespace Nexabode.Views;

public partial class ImpactDialog : Window
{
    private ImpactDialog(string tenantName, string report)
    {
        InitializeComponent();
        Title = $"Review changes - {tenantName}";
        TitleText.Text = Title;
        ReportText.Text = report;
    }

    // True if the administrator chose to apply the change.
    public static bool Confirm(Window? owner, string tenantName, string report)
    {
        var dialog = new ImpactDialog(tenantName, report);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}