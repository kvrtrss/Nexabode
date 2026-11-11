namespace Nexabode.Services;

// The optional "Gender" of a tenant. Boarding houses often need it for deciding who shares a room, but it is
// never required: a tenant can be left as "Not set". Nothing here touches the database or the screens.
public static class GenderRules
{
    public const string NotSet = "Not set";

    // The choices in the drop-down, in the order they are shown. "Not set" is stored as nothing at all.
    public static readonly string[] Choices = { NotSet, "Male", "Female", "Other" };

    // The value to store: one of Male, Female or Other, or null for "Not set", blank or anything unrecognised.
    public static string? Normalize(string? value)
    {
        var text = (value ?? "").Trim();
        foreach (var choice in Choices)
            if (choice != NotSet && string.Equals(choice, text, StringComparison.OrdinalIgnoreCase))
                return choice;
        return null;
    }

    // Blank and "Not set" are fine. Anything else must be one of the choices.
    public static string? Error(string? value)
    {
        var text = (value ?? "").Trim();
        if (text == "" || string.Equals(text, NotSet, StringComparison.OrdinalIgnoreCase)) return null;
        return Normalize(text) == null ? "Choose a gender from the list, or Not set." : null;
    }

    // What to show for a stored value.
    public static string Display(string? stored) => Normalize(stored) ?? NotSet;
}
