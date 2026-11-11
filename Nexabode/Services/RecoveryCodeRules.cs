using System.Security.Cryptography;

namespace Nexabode.Services;

// The recovery code is what lets someone choose a new password after forgetting theirs.
// It is made once, shown once, and only a BCrypt hash of it is kept. No database or screens in here.
public static class RecoveryCodeRules
{
    // 32 letters and digits. 0, 1, I and O are left out because they are easy to misread when written down.
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // 16 characters from 32 is 80 bits: far too many to guess.
    public const int Length = 16;

    // The emergency access key is longer (100 bits) because it can create an Owner account.
    public const int EmergencyKeyLength = 20;

    // A new random code, without dashes (the form that is hashed).
    public static string Generate(int length = Length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    // ABCD-EFGH-JKLM-NPQR, easier to read and write down.
    public static string Format(string code)
    {
        var plain = Normalize(code);
        return string.Join("-", Enumerable.Range(0, (plain.Length + 3) / 4)
            .Select(i => plain.Substring(i * 4, Math.Min(4, plain.Length - i * 4))));
    }

    // What a person typed, turned into the form that is hashed: capitals, with spaces and dashes removed.
    public static string Normalize(string? typed) =>
        new string((typed ?? "").Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool LooksValid(string normalized, int length = Length) =>
        normalized.Length == length && normalized.All(c => Alphabet.Contains(c));
}
