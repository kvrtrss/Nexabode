namespace Nexabode.Services;

public enum RecoverySheetKind { RecoveryCode, EmergencyKey }

// The words on the printed recovery sheet, kept apart from the printing so they can be tested.
// The sheet is for the day someone is locked out, so it says exactly where to go and what to type.
public static class RecoverySheetText
{
    public static string Heading(RecoverySheetKind kind) => kind == RecoverySheetKind.RecoveryCode
        ? "Nexabode recovery code"
        : "Nexabode emergency access key";

    public static string SecretCaption(RecoverySheetKind kind) => kind == RecoverySheetKind.RecoveryCode
        ? "RECOVERY CODE"
        : "EMERGENCY ACCESS KEY";

    public static string Purpose(RecoverySheetKind kind) => kind == RecoverySheetKind.RecoveryCode
        ? "Use this if you forget your password. It lets you choose a new password for one account."
        : "Use this only if you cannot sign in to any account. It creates a new Owner account and changes nothing else.";

    public static IReadOnlyList<string> Steps(RecoverySheetKind kind) => kind == RecoverySheetKind.RecoveryCode
        ? new[]
        {
            "Open Nexabode and, on the sign-in screen, select \"Forgot password?\".",
            "Type your username and the recovery code above.",
            "Choose a new password. Nexabode will then show a new recovery code, so print or write that one down as well."
        }
        : new[]
        {
            "Open Nexabode and, on the sign-in screen, select \"Locked out of every account?\".",
            "Type the emergency access key above (or, if you set a reset phrase in Settings, select \"Reset phrase\" and type that instead), then a username and password for the new Owner account.",
            "Nexabode will then show a new emergency access key. The key on this sheet will stop working, so keep the new one safe."
        };

    public static IReadOnlyList<string> Cautions(RecoverySheetKind kind) => new[]
    {
        kind == RecoverySheetKind.RecoveryCode
            ? "Anyone who has this code can reset the password of this account. Do not share it."
            : "Anyone who has this key can create an Owner account and see everything in Nexabode. Treat it like the key to the building.",
        "Keep this paper somewhere locked, away from the computer that runs Nexabode.",
        "Nexabode cannot show this code or key again, because it stores only a protected copy. If this sheet is lost, create a new one in Settings."
    };

    // The line that says whose code it is. The emergency key belongs to every Owner, so it has no account.
    public static string? AccountLine(RecoverySheetKind kind, string? account) =>
        kind == RecoverySheetKind.RecoveryCode && !string.IsNullOrWhiteSpace(account)
            ? $"Account: {account.Trim()}"
            : null;
}
