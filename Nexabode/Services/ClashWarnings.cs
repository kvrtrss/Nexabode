namespace Nexabode.Services;

// The wording of the clash warnings shown on Rooms & Beds and on the Tenants page, kept apart from the screens
// so it can be tested. A clash is two stays on the same bed on some of the same days.
public static class ClashWarnings
{
    // "Joseph", "Joseph and Kikay", "Joseph, Kikay and Ana".
    public static string JoinNames(IReadOnlyList<string> names) => names.Count switch
    {
        0 => "",
        1 => names[0],
        _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1]
    };

    private static string WhatToDo(bool isOwner, string whereToEdit) =>
        isOwner
            ? $"{whereToEdit} to correct the dates or the bed, or Remove this stay if one was entered by mistake."
            : $"{whereToEdit} to correct the dates or the bed, or ask an Owner to remove one that was entered by mistake.";

    // The red "Check" line on the Tenants page, for a tenant whose bed is also booked by someone else.
    public static string TenantBedWarning(string bed, IReadOnlyList<string> otherTenants, bool isOwner) =>
        $"{bed} is also booked for {JoinNames(otherTenants)} on some of the same days. " +
        WhatToDo(isOwner, "Use Edit this stay under Stay history");

    // Rooms & Beds: the banner at the top of a room that has a clash.
    public static string RoomTitle(int clashingBeds) =>
        clashingBeds == 1 ? "Check: 1 bed has a clash" : $"Check: {clashingBeds} beds have a clash";

    // One line per bed, e.g. "Bed 1: Joseph and Kikay are booked on some of the same days."
    public static string BedLine(string bed, IReadOnlyList<string> tenants) =>
        $"{bed}: {JoinNames(tenants)} {(tenants.Count == 1 ? "is" : "are")} booked on some of the same days.";

    // The buttons are right below, under each "Overlaps with..." line in the list of who is staying.
    public static string RoomAdvice(bool isOwner) =>
        WhatToDo(isOwner, "Use Edit this stay under the tenant's name below");

    // Shown when the pointer rests on the warning mark of a bed.
    public static string BedChipTip(string bed, string status) =>
        $"{bed}: {status}. Two stays overlap on this bed. Select this room to see who and what to do.";
}
