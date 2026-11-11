namespace Nexabode.Services;

// The rules and wording for removing a stay that was entered by mistake (a duplicate, the wrong tenant,
// the wrong bed). No database or screens in here, so it can be tested on its own.
//
// A stay can only go when nothing was ever billed on it: billing records point at the stay, and the books
// must keep adding up. Anything else is ended or corrected, never deleted.
public static class StayRemovalRules
{
    // Null when the stay may be removed. Otherwise the reason it may not, written for the person.
    public static string? BlockedReason(bool hasBilling, decimal depositRefunded)
    {
        if (hasBilling)
            return "This stay cannot be removed because rent or utility charges were already made for it " +
                   "(voided charges count too), and the records of money must stay complete.\n\n" +
                   "If it was entered by mistake, void its unpaid charges in Billing Charges, then use " +
                   "Edit this stay to end it on its move-in date.";

        if (depositRefunded > 0)
            return "This stay cannot be removed because a deposit refund is recorded on it. " +
                   "Select Undo refund for this tenant first, or keep the stay.";

        return null;
    }

    public static string? ReasonError(string? reason) =>
        ReasonRules.Error(reason, "Enter a reason for removing this stay (at least 3 letters or digits). It is saved in the change history.");

    // Shown in the window that asks for the reason.
    public static string FormNote(string tenant, string staySummary, decimal deposit)
    {
        var text = $"Remove this stay of {tenant}?\n\n{staySummary}\n\n" +
                   "The stay will be deleted and the bed will be free for those days. Nothing was billed on it. " +
                   "This cannot be undone, but a record of what was removed is kept in the change history.";

        if (deposit > 0)
            text += $"\n\nA deposit of {Fmt.Peso(deposit)} was recorded for this stay. " +
                    "Nexabode will not keep that record, so settle the deposit separately.";

        return text;
    }

    public static string PasswordNote(string tenant) =>
        $"Enter your password to remove this stay of {tenant}. This cannot be undone.";

    // Two stays of the same tenant on the same bed with the same dates: almost certainly one stay entered twice.
    public static bool IsDuplicate(string bedA, DateOnly moveInA, DateOnly? moveOutA,
                                   string bedB, DateOnly moveInB, DateOnly? moveOutB) =>
        bedA == bedB && moveInA == moveInB && moveOutA == moveOutB;

    // The red "Check" line on a tenant whose stays overlap.
    public static string OverlapWarning(bool hasExactDuplicate, bool isOwner)
    {
        if (hasExactDuplicate)
            return isOwner
                ? "Two of this tenant's stays are exactly the same (same bed and dates), so one is probably a duplicate. " +
                  "Use Remove this stay under Stay history to delete the extra one."
                : "Two of this tenant's stays are exactly the same (same bed and dates), so one is probably a duplicate. " +
                  "Ask an Owner to remove the extra one.";

        return isOwner
            ? "Two of this tenant's stays overlap. Use Edit this stay under Stay history to correct the dates, " +
              "or Remove this stay if one was entered by mistake."
            : "Two of this tenant's stays overlap. Use Edit this stay under Stay history to correct the dates, " +
              "or ask an Owner to remove one that was entered by mistake.";
    }
}
