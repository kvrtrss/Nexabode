namespace Nexabode.Services;

// The reset phrase is a second way back in when no account can be signed in to. Unlike the emergency access key
// it is chosen by an Owner, so it is easier to remember and easier to guess. These rules make it long enough
// to be worth something. Only a BCrypt hash of it is kept. No database or screens in here.
public static class ResetPhraseRules
{
    public const int MinLength = 16;
    public const int MinWords = 3;

    // What a person typed, turned into the form that is hashed: lower case, with extra spaces removed.
    // Capitals and spacing don't matter, so a phrase isn't lost to a stray Shift key or a double space.
    public static string Normalize(string? typed) =>
        string.Join(' ', (typed ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
              .ToLowerInvariant();

    // Null when the phrase can be used, otherwise a sentence that says what to change.
    public static string? Error(string? typed, string? username = null)
    {
        var phrase = Normalize(typed);
        var words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < MinWords || phrase.Length < MinLength)
            return $"Use a phrase of at least {MinWords} words and {MinLength} characters, such as a sentence only you would write.";

        if (words.Distinct().Count() < MinWords)
            return "Use different words rather than repeating the same word.";

        var name = (username ?? "").Trim().ToLowerInvariant();
        if (name.Length >= 3 && phrase.Contains(name))
            return "The phrase must not contain your username.";

        return null;
    }
}
