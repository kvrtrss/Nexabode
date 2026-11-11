using System.Windows;
using System.Windows.Controls;

namespace Nexabode.Views;

public partial class FormDialog : Window
{
    // One entry per field: the value as typed or chosen. Fields with a list of choices are drop-downs.
    private readonly List<(FrameworkElement Input, Func<string> Read)> _inputs = new();
    private readonly Func<string[], string?>? _validate;
    private string[]? _result;

    private FormDialog(string title, string okText, (string Label, string Value)[] fields,
                       Func<string[], string?>? validate, string? note, string[]?[]? choices)
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

        for (var i = 0; i < fields.Length; i++)
        {
            var (label, value) = fields[i];
            FieldPanel.Children.Add(new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("MutedText"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var options = choices != null && i < choices.Length ? choices[i] : null;
            if (options is { Length: > 0 })
            {
                // A fixed list of choices: pick one instead of typing it.
                var combo = new ComboBox
                {
                    ItemsSource = options,
                    SelectedItem = options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase))
                                   ?? options[0],
                    Margin = new Thickness(0, 0, 0, 12),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                System.Windows.Automation.AutomationProperties.SetName(combo, label);
                _inputs.Add((combo, () => combo.SelectedItem as string ?? ""));
                FieldPanel.Children.Add(combo);
            }
            else
            {
                var box = new Wpf.Ui.Controls.TextBox { Text = value, Margin = new Thickness(0, 0, 0, 12) };
                System.Windows.Automation.AutomationProperties.SetName(box, label);
                _inputs.Add((box, () => box.Text));
                FieldPanel.Children.Add(box);
            }
        }

        Loaded += (_, _) =>
        {
            if (_inputs.Count == 0) return;
            var first = _inputs[0].Input;
            first.Focus();
            if (first is Wpf.Ui.Controls.TextBox tb) tb.SelectAll();
        };
    }

    // Shows the form. Returns the typed or chosen values, or null if the user cancelled.
    // 'validate' returns an error message to keep the form open, or null when the values are fine.
    // 'choices' (optional) lines up with 'fields': where an entry is a list, that field is a drop-down
    // of those options instead of a box to type in.
    public static string[]? Ask(Window? owner, string title, string okText,
                                (string Label, string Value)[] fields,
                                Func<string[], string?>? validate = null, string? note = null,
                                string[]?[]? choices = null)
    {
        var dialog = new FormDialog(title, okText, fields, validate, note, choices);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var values = _inputs.Select(i => i.Read()).ToArray();
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
