using System.Windows;
using System.Windows.Controls;

namespace Nexabode.Controls;

public partial class PageHeader : UserControl
{
    public event RoutedEventHandler? ActionClick;

    public PageHeader() => InitializeComponent();

    public string Title
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public string Subtitle
    {
        get => SubtitleText.Text;
        set => SubtitleText.Text = value;
    }

    // Setting this text shows the primary button on the right.
    public string ActionText
    {
        get => ActionButton.Content?.ToString() ?? "";
        set
        {
            ActionButton.Content = value;
            ActionButton.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e) => ActionClick?.Invoke(this, e);
}