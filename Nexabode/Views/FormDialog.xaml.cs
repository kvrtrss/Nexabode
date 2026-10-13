using System.Windows;
using System.Windows.Controls;

namespace Nexabode.Views;

public partial class FormDialog : Window
{
    private readonly List<Wpf.Ui.Controls.TextBox> _boxes = new();
    private readonly Func<string[], string?>? _validate;
    private string[]? _result;

    private FormDialog(string title, string okText, (string Label, string Value)[] fields,
                       Func<string[], string?>? validate, string? note)
    {
        InitializeComponent();
        _validate = validate;
        Title = title;
        TitleText.Text = title;
        OkButton.Content = okText;

        if (!string.IsNullOrWhiteSpace(note))
        {
            NoteText.Text = note;
            NoteText.Visibility = Visibility.Visible;
        }

        foreach (var (label, value) in fields)
        {
            FieldPanel.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(0, 0, 0, 4)
            });
            var box = new Wpf.Ui.Controls.TextBox { Text = value, Margin = new Thickness(0, 0, 0, 12) };
            _boxes.Add(box);
            FieldPanel.Children.Add(box);
        }

        Loaded += (_, _) =>
        {
            if (_boxes.Count == 0) return;
            _boxes[0].Focus();
            _boxes[0].SelectAll();
        };
    }

    // Shows the form. Returns the typed values, or null if the user cancelled.
    // 'validate' returns an error message to keep the form open, or null when the values are fine.
    public static string[]? Ask(Window? owner, string title, string okText,
                                (string Label, string Value)[] fields,
                                Func<string[], string?>? validate = null, string? note = null)
    {
        var dialog = new FormDialog(title, okText, fields, validate, note);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var values = _boxes.Select(b => b.Text).ToArray();
        var error = _validate?.Invoke(values);
        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        _result = values;
        DialogResult = true;
    }
}