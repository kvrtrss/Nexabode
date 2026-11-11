namespace Nexabode.Services;

public enum PasswordStrength { None = 0, Weak = 1, Fair = 2, Good = 3, Strong = 4 }

public readonly record struct PasswordStrengthResult(PasswordStrength Level, string Hint);

// A simple, honest hint about how hard a password is to guess. It only advises: the rules that actually
// block a password (at least 8 characters, not the default, not the username) are in AccountService.
// No database or screens in here, so it can be tested on its own.
public static class PasswordStrengthRules
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password12", "password123", "passw0rd", "p@ssw0rd", "p@ssword",
        "12345678", "123456789", "1234567890", "123123123", "11111111", "00000000", "87654321",
        "qwertyui", "qwerty123", "qwertyuiop", "asdfghjk", "asdfghjkl", "zxcvbnm1",
        "iloveyou", "letmein1", "welcome1", "abc12345", "admin123", "adminadmin",
        "nexabode", "nexabode1", "nexabode123", "boardinghouse"
    };

    public static PasswordStrengthResult Evaluate(string? password, string? username = null)
    {
        password ??= "";
        if (password.Length == 0) return new(PasswordStrength.None, "");

        if (password.Length < 8)
            return new(PasswordStrength.Weak, "Use at least 8 characters.");

        if (Common.Contains(password))
            return new(PasswordStrength.Weak, "This is a very common password. Choose something harder to guess.");

        var name = (username ?? "").Trim();
        if (name.Length >= 3 && password.Contains(name, StringComparison.OrdinalIgnoreCase))
            return new(PasswordStrength.Weak, "Do not include your username in your password.");

        if (password.Distinct().Count() <= 3 || IsSequence(password))
            return new(PasswordStrength.Weak, "Avoid repeated or sequential characters.");

        var classes = (password.Any(char.IsLower) ? 1 : 0)
                    + (password.Any(char.IsUpper) ? 1 : 0)
                    + (password.Any(char.IsDigit) ? 1 : 0)
                    + (password.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);

        // A long password of any kind is strong: length matters more than symbols.
        if (password.Length >= 16 && password.Distinct().Count() >= 6)
            return new(PasswordStrength.Strong, "Strong password.");
        if (password.Length >= 12 && classes >= 3)
            return new(PasswordStrength.Strong, "Strong password.");

        if (password.Length >= 12 || (password.Length >= 10 && classes >= 3))
            return new(PasswordStrength.Good, "A few more characters would make it stronger.");

        return new(PasswordStrength.Fair, "Make it longer, or mix letters, numbers and symbols.");
    }

    public static string Label(PasswordStrength level) => level switch
    {
        PasswordStrength.Weak => "Weak",
        PasswordStrength.Fair => "Fair",
        PasswordStrength.Good => "Good",
        PasswordStrength.Strong => "Strong",
        _ => ""
    };

    // 12345678, abcdefgh, 87654321: every character is one step from the one before.
    private static bool IsSequence(string text)
    {
        var up = true;
        var down = true;
        for (var i = 1; i < text.Length; i++)
        {
            var step = char.ToLowerInvariant(text[i]) - char.ToLowerInvariant(text[i - 1]);
            if (step != 1) up = false;
            if (step != -1) down = false;
        }
        return up || down;
    }
}
