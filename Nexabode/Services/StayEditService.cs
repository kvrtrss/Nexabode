using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

// The values an administrator can change on a stay, plus why.
public sealed class StayEdit
{
    public int BedId { get; set; }
    public DateOnly MoveIn { get; set; }
    public DateOnly? MoveOut { get; set; }
    public decimal Rent { get; set; }
    public decimal Deposit { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool ConflictException { get; set; }   // saved despite an overlap
}

public static class StayEditService
{
    public const string NoMoveOut = "(none - ongoing)";

    public static string DateText(DateOnly? date) =>
        date is DateOnly d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : NoMoveOut;

    public static string MoneyText(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string BedName(AppDbContext db, int bedId)
    {
        var bed = db.Beds.Include(b => b.Room).FirstOrDefault(b => b.Id == bedId);
        return bed == null ? $"Bed #{bedId}" : $"{bed.Room.RoomName} / {bed.BedLabel}";
    }

    public static string Summary(AppDbContext db, Occupancy o) =>
        $"{BedName(db, o.BedId)}, {DateText(o.MoveInDate)} to {DateText(o.MoveOutDate)}, " +
        $"rent {MoneyText(o.AgreedRent)}, deposit {MoneyText(o.Deposit)}";

    // Null when the change creates no new overlap. Otherwise a message naming each stay it would newly
    // overlap (or overlap for longer) and the days. 'stay' must still hold its current values.
    public static string? DescribeConflicts(AppDbContext db, Occupancy stay, int bedId,
                                            DateOnly moveIn, DateOnly? moveOut)
    {
        var proposed = new StayRange(stay.Id, moveIn, moveOut);
        var bed = StayConflicts.NewOnBed(db, stay, bedId, proposed);
        var tenant = StayConflicts.NewForTenant(db, stay, proposed);
        if (bed.Count == 0 && tenant.Count == 0) return null;

        var parts = new List<string>();
        if (bed.Count > 0)
            parts.Add("Another tenant would be in this bed on the same day:\n" + StayConflicts.Describe(bed));
        if (tenant.Count > 0)
            parts.Add("This tenant would be in two beds on the same day:\n" + StayConflicts.Describe(tenant));
        return string.Join("\n\n", parts);
    }

    // Applies the edit to the tracked stay and adds one audit entry per changed field.
    // The caller saves, so the change and its audit entries succeed or fail together.
    // Returns how many fields changed.
    public static int Apply(AppDbContext db, Occupancy stay, StayEdit edit)
    {
        var changes = 0;

        void Change(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            AuditService.Record(db, "Stay", stay.Id, stay.TenantId, field, oldValue, newValue,
                                edit.Reason, edit.ConflictException);
            changes++;
        }

        Change("Bed", BedName(db, stay.BedId), BedName(db, edit.BedId));
        Change("MoveInDate", DateText(stay.MoveInDate), DateText(edit.MoveIn));
        Change("MoveOutDate", DateText(stay.MoveOutDate), DateText(edit.MoveOut));
        Change("AgreedRent", MoneyText(stay.AgreedRent), MoneyText(edit.Rent));
        Change("Deposit", MoneyText(stay.Deposit), MoneyText(edit.Deposit));

        if (changes == 0) return 0;
        // A new move-out date replaces an earlier "moved out" confirmation, so the status follows the dates again.
        if (stay.MoveOutDate != edit.MoveOut) stay.MovedOutOn = null;
        stay.BedId = edit.BedId;
        stay.MoveInDate = edit.MoveIn;
        stay.MoveOutDate = edit.MoveOut;
        stay.AgreedRent = edit.Rent;
        stay.Deposit = edit.Deposit;
        return changes;
    }

    // A copy of a stay's current values, so a proposed change can be previewed before it is applied.
    public static StayEdit Snapshot(Occupancy o) => new()
    {
        BedId = o.BedId,
        MoveIn = o.MoveInDate,
        MoveOut = o.MoveOutDate,
        Rent = o.AgreedRent,
        Deposit = o.Deposit
    };

}