namespace Nexabode.Services;

// The rules and wording for bringing a voided rent charge back. No database or screens in here,
// so it can be tested on its own.
//
// A voided rent charge counts as "already generated" and is never created again, because voiding can be a
// deliberate choice (a free month, a charge made in error). When the choice was wrong, or the stay changed
// afterwards, the charge is restored: the same record becomes active again, updated to the stay as it is now.
public static class RentRestoreRules
{
    // Null when the charge may be restored. Otherwise the reason it may not, written for the person.
    public static string? BlockedReason(bool isVoided, bool hasPayments, bool hasAdjustments, bool stayHasDaysInMonth)
    {
        if (!isVoided)
            return "This rent charge is not voided, so there is nothing to restore.";

        if (hasPayments)
            return "This charge still has payments applied to it, so it cannot be restored here. " +
                   "Review it on Billing Charges and Payments.";

        if (hasAdjustments)
            return "This charge has adjustment charges linked to it, so it cannot be restored automatically. " +
                   "Review it on Billing Charges.";

        if (!stayHasDaysInMonth)
            return "The stay has no days in that month, so there is no rent to restore.";

        return null;
    }

    public static string? ReasonError(string? reason) =>
        ReasonRules.Error(reason, "Enter a reason for restoring the charge (at least 3 letters or digits). It is saved in the change history.");

    // Shown in the window that asks for the reason.
    public static string ConfirmNote(string tenant, string month, decimal oldAmount, string? oldDescription,
                                     decimal newAmount, string newDescription, DateOnly newDueDate) =>
        $"Restore the {month} rent of {tenant}?\n\n" +
        $"The voided charge ({(string.IsNullOrWhiteSpace(oldDescription) ? "Monthly rent" : oldDescription)}, " +
        $"{Fmt.Peso(oldAmount)}) becomes active again, updated to match the stay as it is now:\n" +
        $"{newDescription}: {Fmt.Peso(newAmount)}, due {Fmt.Date(newDueDate)}.\n\n" +
        "It counts in the totals again and can be paid. The change is recorded in the change history.";

    // Added to the message after a stay is edited or ended, when that gives days back to a month whose rent
    // was voided: the voided charge is left alone, so the person has to be told where to restore it.
    public static string VoidedMonthNote(string month) =>
        $"Note: the {month} rent was voided earlier, so it was not billed again. If it should be billed, " +
        "open Rent Billing, tick Show voided, select it and use Restore charge.";
}
