namespace Nexabode.Services;

public static class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    // Null if the username is acceptable, otherwise a message for the person. Surrounding spaces are ignored.
    public static string? Validate(string username)
    {
        var name = username.Trim();
        if (name.Length < MinLength) return $"The username must be at least {MinLength} characters.";
        if (name.Length > MaxLength) return $"The username can be at most {MaxLength} characters.";
        if (!name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            return "Use only letters, numbers, dots, dashes and underscores (no spaces).";
        return null;
    }

    // The letter shown in the round badge beside the name.
    public static string Initial(string username) =>
        string.IsNullOrWhiteSpace(username) ? "A" : char.ToUpperInvariant(username.Trim()[0]).ToString();
}