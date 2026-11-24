using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// What the tenant form holds. Text as typed or chosen; nothing is trimmed or checked here.
public class TenantValues
{
    public string FullName { get; set; } = "";
    public string Gender { get; set; } = GenderRules.NotSet;
    public string Contact { get; set; } = "";
    public string EmergencyContact { get; set; } = "";
    public string SchoolOrWork { get; set; } = "";
    public string Notes { get; set; } = "";
}

// The window for Add tenant and Edit tenant. It is laid out like Edit stay (same width, title, labels, red message
// banner and buttons) so tenant, stay and deposit windows all look alike.
public partial class TenantDialog : Window
{
    private readonly Func<TenantValues, string?>? _validate;
    private TenantValues? _result;

    private TenantDialog(string title, string okText, string? editing, TenantValues current,
                         Func<TenantValues, string?>? validate)
    {
        InitializeComponent();
        _validate = validate;

        Title = title;
        TitleText.Text = title;
        OkButton.Content = okText;

        if (!string.IsNullOrWhiteSpace(editing))
        {
            NoteText.Text = $"You are changing the record of {editing}.";
            NoteText.Visibility = Visibility.Visible;
        }

        GenderCombo.ItemsSource = GenderRules.Choices;
        GenderCombo.SelectedItem = GenderRules.Choices.FirstOrDefault(
            c => string.Equals(c, current.Gender, StringComparison.OrdinalIgnoreCase)) ?? GenderRules.NotSet;

        NameBox.Text = current.FullName;
        ContactBox.Text = current.Contact;
        EmergencyBox.Text = current.EmergencyContact;
        SchoolBox.Text = current.SchoolOrWork;
        NotesBox.Text = current.Notes;

        System.Windows.Automation.AutomationProperties.SetName(NameBox, "Full name");
        System.Windows.Automation.AutomationProperties.SetName(GenderCombo, "Gender");
        System.Windows.Automation.AutomationProperties.SetName(ContactBox, "Contact number");
        System.Windows.Automation.AutomationProperties.SetName(EmergencyBox, "Emergency contact");
        System.Windows.Automation.AutomationProperties.SetName(SchoolBox, "School or work");
        System.Windows.Automation.AutomationProperties.SetName(NotesBox, "Notes");

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    // Shows the form. Returns the confirmed values, or null if the administrator cancelled.
    // 'validate' returns an error message to keep the window open, or null when the values are fine.
    // 'editing' is the tenant's name when editing (shown as a note), or null when adding.
    public static TenantValues? Ask(Window? owner, string title, string okText, string? editing,
                                    TenantValues current, Func<TenantValues, string?>? validate = null)
    {
        var dialog = new TenantDialog(title, okText, editing, current, validate);
        if (owner != null) dialog.Owner = owner;
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var values = new TenantValues
        {
            FullName = NameBox.Text,
            Gender = GenderCombo.SelectedItem as string ?? GenderRules.NotSet,
            Contact = ContactBox.Text,
            EmergencyContact = EmergencyBox.Text,
            SchoolOrWork = SchoolBox.Text,
            Notes = NotesBox.Text
        };

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
