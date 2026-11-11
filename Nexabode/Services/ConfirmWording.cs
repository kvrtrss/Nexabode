namespace Nexabode.Services;

// What the "Are you sure?" windows say. Kept apart from the screens so it can be tested.
// Nothing here touches the database or opens a window.
public static class ConfirmWording
{
    // A blank value is shown as "(empty)" so a cleared field is visible in the list of changes.
    public static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();

    // The fields that really changed, one line each. Spaces around a value do not count as a change.
    public static List<string> Changes(params (string Field, string? Old, string? New)[] fields) =>
        fields
            .Where(f => (f.Old ?? "").Trim() != (f.New ?? "").Trim())
            .Select(f => $"{f.Field}: {Show(f.Old)} to {Show(f.New)}")
            .ToList();

    private static string Edit(string what, IReadOnlyList<string> changes) =>
        $"Save these changes to {what}?\n\n" + string.Join("\n", changes.Select(c => "- " + c));

    public static string EditTenant(string tenant, IReadOnlyList<string> changes) =>
        Edit($"{tenant}'s record", changes);

    public static string EditRoom(string room, IReadOnlyList<string> changes) => Edit(room, changes);

    // Any other edit: "Save these changes to {what}?" and the list of what changes.
    public static string EditRecord(string what, IReadOnlyList<string> changes) => Edit(what, changes);

    public static string EditBed(string room, string bed, IReadOnlyList<string> changes) =>
        Edit($"{room}, {bed}", changes);

    // ---------- Statuses ----------

    public static string RoomStatus(string room, bool activate) => activate
        ? $"Activate {room}?\n\nIts beds can be given to tenants again."
        : $"Deactivate {room}?\n\nIts beds cannot be given to new tenants while it is inactive. " +
          "You can activate it again at any time.";

    public static string BedStatus(string room, string bed, bool activate) => activate
        ? $"Activate {room}, {bed}?\n\nIt can be given to tenants again."
        : $"Deactivate {room}, {bed}?\n\nIt cannot be given to new tenants while it is inactive. " +
          "You can activate it again at any time.";

    public static string MaintenanceStart(string room, string bed) =>
        $"Put {room}, {bed} under maintenance?\n\nIt cannot be given to new tenants until you end the maintenance.";

    public static string MaintenanceEnd(string room, string bed) =>
        $"End the maintenance on {room}, {bed}?\n\nIt can be given to tenants again.";

    // ---------- Credit (a tenant's deposit that has not been used) ----------

    public static string ApplyCredit(string tenant, decimal amount, decimal creditBefore, decimal balanceBefore) =>
        $"Use {Fmt.Peso(amount)} of {tenant}'s credit to pay unpaid charges?\n\n" +
        $"Credit: {Fmt.Peso(creditBefore)} to {Fmt.Peso(creditBefore - amount)}\n" +
        $"Unpaid balance: {Fmt.Peso(balanceBefore)} to {Fmt.Peso(balanceBefore - amount)}\n\n" +
        "It is paid against the oldest unpaid charges first, and recorded as a payment with the method \"Credit\".";

    public static string RefundCredit(string tenant, decimal amount, decimal creditBefore) =>
        $"Refund {Fmt.Peso(amount)} of credit to {tenant}?\n\n" +
        $"Credit: {Fmt.Peso(creditBefore)} to {Fmt.Peso(creditBefore - amount)}\n\n" +
        "This records money handed back to the tenant. If it was a mistake, use Undo refund.";

    public static string UndoRefund(string tenant, decimal putBack, decimal creditBefore) =>
        $"Put {Fmt.Peso(putBack)} back into {tenant}'s credit?\n\n" +
        $"Credit: {Fmt.Peso(creditBefore)} to {Fmt.Peso(creditBefore + putBack)}";

    public static string EditCredit(string tenant, decimal before, decimal after, string reason) =>
        $"Change {tenant}'s credit from {Fmt.Peso(before)} to {Fmt.Peso(after)}?\n\n" +
        $"Reason: {reason.Trim()}\n\nThe change is saved in the change history.";

    // ---------- Accounts and passwords ----------

    public static string CreateOwner(string username) =>
        $"Create the Owner account \"{username}\"?\n\n" +
        "An Owner can manage accounts and restore backups. The account is created as soon as you confirm.";

    public static string ChangePassword(string username) =>
        $"Change the password for \"{username}\"?\n\nYou will use the new password the next time you sign in.";

    public static string ChangeUsername(string currentUsername, string newUsername) =>
        $"Change your username from \"{currentUsername}\" to \"{newUsername}\"?\n\n" +
        "Use the new username the next time you sign in. Earlier entries in the change history keep your old name.";

    public static string RecoverPassword(string username) =>
        $"Set a new password for \"{username}\" using the recovery code?\n\n" +
        "The recovery code is used up and a new one is created and shown to you next.";

    // The first sign-in: a new password, and maybe a new username too.
    public static string FirstSignIn(string currentUsername, string chosenUsername) =>
        string.Equals(currentUsername, chosenUsername, StringComparison.Ordinal)
            ? "Save your new password?\n\nYou will use it the next time you sign in."
            : $"Save your new username \"{chosenUsername}\" and your new password?\n\n" +
              "You will use them the next time you sign in.";

    // ---------- Roles ----------

    public static string RoleChoiceNote(string username, string currentRole) =>
        $"{username} is now {currentRole}. Choose the role to change to.\n\n" +
        "Staff can work with tenants, rooms, billing, payments and reports. " +
        "An Owner can also manage accounts and restore backups.";

    public static string? RoleChoiceError(string username, string currentRole, string? chosenRole) =>
        chosenRole == null ? "Choose a role."
        : chosenRole == currentRole ? $"{username} is already {currentRole}. Choose the other role."
        : null;
}
