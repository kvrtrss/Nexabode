using System.ComponentModel;
using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// Shows the Owner recovery key one time. It is not put on the clipboard and is not saved anywhere readable.
public partial class RecoveryCodeDialog : Window
{
    private sealed record Texts(string Title, string Note, string Caption, string WarnTitle,
                                string WarnBody, string CheckText);

    private readonly string _code;
    private readonly Func<string, string?>? _saveOnContinue;
    private readonly bool _mustAcknowledge;
    private bool _finished;

    private RecoveryCodeDialog(string code, Texts texts, Func<string, string?>? saveOnContinue, bool mustAcknowledge)
    {
        InitializeComponent();
        _code = code;
        _saveOnContinue = saveOnContinue;
        _mustAcknowledge = mustAcknowledge;

        Title = texts.Title;
        TitleText.Text = texts.Title;
        NoteText.Text = texts.Note;
        CaptionText.Text = texts.Caption;
        WarnTitle.Text = texts.WarnTitle;
        WarnBody.Text = texts.WarnBody;
        SavedCheck.Content = texts.CheckText;
        CodeText.Text = RecoveryCodeRules.Format(code);
        LaterButton.Visibility = mustAcknowledge ? Visibility.Collapsed : Visibility.Visible;

        // The window can't be closed with the X until the key has been acknowledged.
        Closing += OnClosing;
        Loaded += (_, _) => SavedCheck.Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_mustAcknowledge && !_finished) e.Cancel = true;
    }

    private static Texts KeyTexts(string title, string note) => new(
        title, note, "Owner recovery key", "Shown only once",
        "Write it down or print it, and keep it somewhere secure, away from this computer, such as a locked drawer. " +
        "Anyone who has it can take over an Owner account and see everything in Nexabode, so treat it like the key " +
        "to the building. Nexabode cannot show it again, but you can create a new one in Settings at any time.",
        "I have written down or printed my recovery key");

    // The replacement key made when the old one was used. It is already saved, so it must be acknowledged.
    public static void ShowKeyIssued(Window? owner, string key)
    {
        var dialog = new RecoveryCodeDialog(key, KeyTexts("Your new recovery key",
            "The key you just used no longer works. Keep this new key instead."),
            saveOnContinue: null, mustAcknowledge: true);
        if (owner != null) dialog.Owner = owner;
        dialog.ShowDialog();
    }

    // First-run setup: makes the key and shows it. 'save' is called when the person confirms they have written it
    // down, and creates the account together with the key. It returns an error message, or null when it worked.
    // "Back" saves nothing. Returns true when it was saved.
    public static bool IssueOwnerKey(Window? owner, Func<string, string?> save)
    {
        var key = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
        var dialog = new RecoveryCodeDialog(key, KeyTexts("Save your Owner recovery key",
            "This is the spare key to your Owner account. If you ever forget your password, it lets you choose " +
            "a new one from the sign-in screen. Your account is created when you continue."),
            save, mustAcknowledge: false);
        if (owner != null) dialog.Owner = owner;
        dialog.LaterButton.Content = "Back";

        return dialog.ShowDialog() == true;
    }

    // Makes a new key (Owners only). It is only saved when the person confirms they have written it down,
    // so choosing "Not now" leaves any existing key working. Returns true when a new key was saved.
    public static bool IssueKey(Window? owner, string reason, bool firstTime = false)
    {
        var title = firstTime ? "Save your Owner recovery key" : "New recovery key";
        var note = firstTime
            ? "This is the spare key to your Owner account. If you ever forget your password, it lets you choose " +
              "a new one from the sign-in screen. This takes about a minute, and you can also do it later from the Dashboard."
            : "Your current recovery key will stop working as soon as you continue.";

        var key = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
        var dialog = new RecoveryCodeDialog(key, KeyTexts(title, note),
            saveOnContinue: k =>
            {
                try { AuthService.SaveEmergencyKey(k, reason); return null; }
                catch (Exception ex) { return "The recovery key could not be saved. Details: " + ex.Message; }
            },
            mustAcknowledge: false);
        if (owner != null) dialog.Owner = owner;
        dialog.LaterButton.Content = firstTime ? "Not now" : "Cancel";

        return dialog.ShowDialog() == true;
    }

    // For an Owner who has no recovery key yet (for example an account made before this version): offers to make one.
    public static bool OfferIfMissing(Window? owner)
    {
        if (!Session.IsOwner || AuthService.HasEmergencyKey()) return false;
        return IssueKey(owner, "First recovery key made at setup", firstTime: true);
    }

    private void Print_Click(object sender, RoutedEventArgs e) =>
        RecoverySheetPrinter.Print(this, RecoverySheetKind.EmergencyKey, null, RecoveryCodeRules.Format(_code));

    private void SavedCheck_Changed(object sender, RoutedEventArgs e) =>
        ContinueButton.IsEnabled = SavedCheck.IsChecked == true;

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (_saveOnContinue != null)
        {
            var error = _saveOnContinue(_code);
            if (error != null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }

        _finished = true;
        DialogResult = true;
    }
}
