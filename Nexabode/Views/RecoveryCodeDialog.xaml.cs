using System.ComponentModel;
using System.Windows;
using Nexabode.Services;

namespace Nexabode.Views;

// Shows a recovery code or an emergency access key one time. It is not put on the clipboard and is
// not saved anywhere readable.
public partial class RecoveryCodeDialog : Window
{
    // The wording that changes with the kind of secret being shown.
    private sealed record Texts(string Title, string Note, string Caption, string WarnTitle,
                                string WarnBody, string CheckText);

    private readonly string _code;
    private readonly RecoverySheetKind _kind;
    private readonly string? _account;
    private readonly Func<string, string?>? _saveOnContinue;
    private readonly bool _mustAcknowledge;
    private bool _finished;

    private RecoveryCodeDialog(string code, RecoverySheetKind kind, string? account, Texts texts,
                               Func<string, string?>? saveOnContinue, bool mustAcknowledge)
    {
        InitializeComponent();
        _code = code;
        _kind = kind;
        _account = account;
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

        // The window can't be closed with the X until the code has been acknowledged.
        Closing += OnClosing;
        Loaded += (_, _) => SavedCheck.Focus();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_mustAcknowledge && !_finished) e.Cancel = true;
    }

    // ---------- recovery code ----------

    private static Texts RecoveryTexts(string title, string note) => new(
        title, note, "RECOVERY CODE", "Shown only once",
        "Write it down or print it, and keep it somewhere private, away from this computer. Anyone who has it " +
        "can reset your password, so do not share it. Nexabode cannot show it again, but you can create a new one " +
        "in Settings at any time.",
        "I have written down or printed my recovery code");

    // A code that is already saved (made while changing a password). It must be acknowledged before closing.
    // 'account' is whose code it is, for the printed sheet.
    public static void ShowIssued(Window? owner, string code, string? account = null)
    {
        var dialog = new RecoveryCodeDialog(code, RecoverySheetKind.RecoveryCode, account,
            RecoveryTexts("Your new recovery code",
                          "Your previous recovery code no longer works. Keep this new code instead."),
            saveOnContinue: null, mustAcknowledge: true);
        if (owner != null) dialog.Owner = owner;
        dialog.ShowDialog();
    }

    // Makes a new code. It is only saved when the person confirms they have written it down,
    // so choosing "Not now" leaves any existing code working. Returns true when a new code was saved.
    public static bool Issue(Window? owner, int userId, string reason, bool firstTime = false)
    {
        var title = firstTime ? "Save your recovery code" : "New recovery code";
        var note = firstTime
            ? "Think of it as a spare key for your account. If you ever forget your password, this code lets you " +
              "choose a new one from the sign-in screen. This takes about a minute, and you can also do it later from the Dashboard."
            : "Your current recovery code will stop working as soon as you continue.";

        var dialog = new RecoveryCodeDialog(RecoveryCodeRules.Generate(), RecoverySheetKind.RecoveryCode,
            AuthService.UsernameOf(userId), RecoveryTexts(title, note),
            saveOnContinue: c =>
            {
                try { AuthService.SaveRecoveryCode(userId, c, reason); return null; }
                catch (Exception ex) { return "The recovery code could not be saved. Details: " + ex.Message; }
            },
            mustAcknowledge: false);
        if (owner != null) dialog.Owner = owner;
        dialog.LaterButton.Content = firstTime ? "Not now" : "Cancel";

        return dialog.ShowDialog() == true;
    }

    // ---------- emergency access key ----------

    private static Texts EmergencyTexts(string title, string note) => new(
        title, note, "EMERGENCY ACCESS KEY", "Shown only once",
        "Write it down or print it, and keep it somewhere secure, away from this computer, such as a locked drawer. " +
        "Anyone who has it can create an Owner account and see everything in Nexabode, so treat it like the key " +
        "to the building. Nexabode cannot show it again, but you can create a new one in Settings at any time.",
        "I have written down or printed my emergency access key");

    // The replacement key made when the old one was used. It is already saved, so it must be acknowledged.
    public static void ShowEmergencyIssued(Window? owner, string key)
    {
        var dialog = new RecoveryCodeDialog(key, RecoverySheetKind.EmergencyKey, null,
            EmergencyTexts("Your new emergency access key",
                           "The key you just used no longer works. Keep this new key instead."),
            saveOnContinue: null, mustAcknowledge: true);
        if (owner != null) dialog.Owner = owner;
        dialog.ShowDialog();
    }

    // Makes a new emergency access key (Owners only). It is only saved when the person confirms they have
    // written it down. Returns true when a new key was saved.
    public static bool IssueEmergencyKey(Window? owner, string reason, bool firstTime = false)
    {
        var title = firstTime ? "Save your emergency access key" : "New emergency access key";
        var note = firstTime
            ? "This is a spare key for the rare case that nobody can sign in. It lets you create a new Owner account " +
              "from the sign-in screen. You can also do this later from the Dashboard."
            : "Your current emergency access key will stop working as soon as you continue.";

        var key = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
        var dialog = new RecoveryCodeDialog(key, RecoverySheetKind.EmergencyKey, null, EmergencyTexts(title, note),
            saveOnContinue: k =>
            {
                try { AuthService.SaveEmergencyKey(k, reason); return null; }
                catch (Exception ex) { return "The emergency access key could not be saved. Details: " + ex.Message; }
            },
            mustAcknowledge: false);
        if (owner != null) dialog.Owner = owner;
        dialog.LaterButton.Content = firstTime ? "Not now" : "Cancel";

        return dialog.ShowDialog() == true;
    }

    private void Print_Click(object sender, RoutedEventArgs e) =>
        RecoverySheetPrinter.Print(this, _kind, _account, RecoveryCodeRules.Format(_code));

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
