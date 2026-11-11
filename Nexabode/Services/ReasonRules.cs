namespace Nexabode.Services;

// A reason typed for a change ends up in the change history, where other people read it later. "." or "j"
// tells them nothing, so a reason must have at least three letters or digits. No database or screens in here.
public static class ReasonRules
{
    public const int MinimumCharacters = 3;

    public const string Hint = "Enter a reason of at least 3 letters or digits. It is saved in the change history.";

    // True when the text has enough real characters (letters or digits) to explain something.
    public static bool IsReal(string? reason) =>
        (reason ?? "").Count(char.IsLetterOrDigit) >= MinimumCharacters;

    // Null when the reason is fine. Otherwise the message to show, which is 'message' when given.
    public static string? Error(string? reason, string? message = null) =>
        IsReal(reason) ? null : message ?? Hint;
}
