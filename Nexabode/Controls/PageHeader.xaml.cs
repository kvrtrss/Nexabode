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

    // Used by the Ctrl+N shortcut: does what the main button does, if it is there and switched on.
    public bool TryInvokeAction()
    {
        if (ActionButton.Visibility != Visibility.Visible || !ActionButton.IsEnabled) return false;
        ActionButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        return true;
    }

    // Switch the primary button off when there is nothing for it to do.
    public bool ActionEnabled
    {
        get => ActionButton.IsEnabled;
        set => ActionButton.IsEnabled = value;
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e) => ActionClick?.Invoke(this, e);
}