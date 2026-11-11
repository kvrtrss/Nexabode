namespace Nexabode.Services;

// The rules and wording for cancelling a stay that has billing records and so cannot be deleted.
// A cancelled stay is kept with all its records, but it is treated as if it never held a bed: it no longer
// shows as an occupant, causes no clash and gets no rent. No database or screens in here.
//
// It can only be cancelled when nothing active is left on it, so no money is hidden by cancelling:
// every charge must already be voided (a charge with a payment needs the payment voided first),
// and no deposit may be recorded on it.
public static class StayCancelRules
{
    // Null when the stay may be cancelled. Otherwise the reason it may not, written for the person.
    public static string? BlockedReason(bool hasActiveCharges, decimal deposit)
    {
        if (hasActiveCharges)
            return "This stay still has active charges, so it cannot be removed yet.\n\n" +
                   "Void them first in Billing Charges (a charge that already has a payment needs that payment " +
                   "voided first), then use Remove this stay again.";

        if (deposit > 0)
            return $"This stay has a deposit of {Fmt.Peso(deposit)} recorded, and removing the stay would take " +
                   "that record with it.\n\n" +
                   "If the deposit was never collected, set it to zero with Edit this stay. " +
                   "If it was collected, refund it first.";

        return null;
    }

    public static string? ReasonError(string? reason) =>
        ReasonRules.Error(reason, "Enter a reason for removing this stay (at least 3 letters or digits). It is saved in the change history.");

    // Shown in the window that asks for the reason.
    public static string FormNote(string tenant, string staySummary) =>
        $"Remove this stay of {tenant}?\n\n{staySummary}\n\n" +
        "Rent or utility charges were made for this stay in the past, so it cannot be deleted. It will be " +
        "cancelled instead: it no longer holds the bed, causes no clash and is not billed again. It stays in " +
        "Stay history marked as Cancelled, and its voided charges stay on record. " +
        "This cannot be undone from the app.";

    public static string PasswordNote(string tenant) =>
        $"Enter your password to remove this stay of {tenant}. This cannot be undone.";

    // The grey line under a cancelled stay in Stay history.
    public static string HistoryLine(DateOnly? cancelledOn, string? reason)
    {
        var when = cancelledOn is DateOnly d ? $" on {Fmt.Date(d)}" : "";
        return string.IsNullOrWhiteSpace(reason) ? $"Cancelled{when}" : $"Cancelled{when}: {reason.Trim()}";
    }
}
