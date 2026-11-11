namespace Nexabode.Services;

// The plain-language sentences of the "Review changes" window, kept apart from the screens so they can be
// tested. Nothing here touches the database.
public static class ImpactWording
{
    // One sentence saying what the whole change means for the rent a tenant owes.
    public static string NetRentSentence(decimal netChange, bool needsDecision, bool hasUtilityChanges)
    {
        string sentence;
        if (needsDecision)
            sentence = "Some items below need your decision. They are left exactly as they are.";
        else if (netChange > 0)
            sentence = $"The tenant will owe {Fmt.Peso(netChange)} more in rent.";
        else if (netChange < 0)
            sentence = $"The tenant will owe {Fmt.Peso(-netChange)} less in rent.";
        else
            sentence = "The rent owed does not change.";

        if (hasUtilityChanges) sentence += " Utility shares change too: see below.";
        return sentence;
    }

    // The month needs more rent than was billed, and the original charge cannot be changed (it has payments),
    // so the difference is added as a separate adjustment charge.
    public static string MoreRentDue(string month, decimal newAmount, string? description, decimal difference,
                                     decimal currentTotal, decimal paidTotal, string days, bool fullMonthPolicyApplies)
    {
        var detail = string.IsNullOrWhiteSpace(description) ? "" : $" ({description})";
        var paid = paidTotal > 0 ? $" ({Fmt.Peso(paidTotal)} paid)" : "";

        var text = $"{month}: the rent for this month becomes {Fmt.Peso(newAmount)}{detail}, which is " +
                   $"{Fmt.Peso(difference)} more than the {Fmt.Peso(currentTotal)} already billed. " +
                   $"The existing charge stays as billed{paid}, and an adjustment charge of " +
                   $"{Fmt.Peso(difference)} is added ({days}).";

        // A shorter stay that costs more looks like a mistake, so the reason is said out loud.
        if (fullMonthPolicyApplies)
            text += " A shorter stay normally costs less, but your settings charge the full month's rent " +
                    "when a stay does not cover the whole month.";
        return text;
    }

    // Line markers understood by the Review changes window. A line starting with one of these is drawn
    // as a heading, a sub-heading, a highlighted summary, a bullet or a small grey note. Other lines are plain text.
    public const string Heading = "## ";
    public const string SubHeading = "### ";
    public const string Summary = "!! ";
    public const string Note = "~ ";
}
