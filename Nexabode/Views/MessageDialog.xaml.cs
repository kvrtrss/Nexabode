using System.Windows;
using System.Windows.Input;

namespace Nexabode.Views;

// A plain message in Nexabode's own style (and dark mode), instead of the grey Windows message box.
public partial class MessageDialog : Window
{
    public MessageDialog(string? heading, string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        if (!string.IsNullOrEmpty(heading))
        {
            HeadingText.Text = heading;
            HeadingText.Visibility = Visibility.Visible;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
