using System.Windows;

namespace Nexabode.Views;

// A plain "Are you sure?" step before a change is made. Confirm goes ahead, Back (or Esc) changes nothing.
// Enter confirms. Used for every confirmation in the app, so they all look and behave the same.
public partial class ConfirmActionDialog : Window
{
    private ConfirmActionDialog(string title, string message, string confirmText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
    }

    // True only when the person pressed the confirm button. 'confirmText' names the action ("Archive", "Delete"...).
    public static bool Ask(Window? owner, string title, string message, string confirmText = "Confirm")
    {
        var dialog = new ConfirmActionDialog(title, message, confirmText);
        dialog.Owner = owner ?? ActiveWindow();
        return dialog.ShowDialog() == true;
    }

    // A drop-in for MessageBox.Show(message, title, YesNo): Yes when confirmed, No otherwise.
    public static MessageBoxResult YesNo(string message, string title, Window? owner = null, string confirmText = "Confirm") =>
        Ask(owner, title, message, confirmText) ? MessageBoxResult.Yes : MessageBoxResult.No;

    // With no owner given, the window that is in front: so the dialog opens over the screen the person is using.
    private static Window? ActiveWindow()
    {
        var app = Application.Current;
        if (app == null) return null;
        foreach (Window w in app.Windows)
            if (w.IsActive && w.IsVisible) return w;
        return app.MainWindow is { IsVisible: true } main ? main : null;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
