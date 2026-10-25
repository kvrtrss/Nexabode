using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public sealed record StayConflict(int OccupancyId, string TenantName, string BedName,
                                  StayRange Stay, DateSpan Overlap);

public static class StayConflicts
{
    public static List<StayConflict> ForBed(AppDbContext db, int bedId, StayRange proposed) =>
        Find(db.Occupancies.Where(o => o.BedId == bedId), proposed);

    public static List<StayConflict> ForTenant(AppDbContext db, int tenantId, StayRange proposed) =>
        Find(db.Occupancies.Where(o => o.TenantId == tenantId), proposed);

    // Overlaps on the bed that this change would CREATE or MAKE LONGER. An overlap that already exists
    // and does not grow is left out, so ending or shortening a stay is never blocked by it.
    // 'stay' must still hold its current (unchanged) values.
    public static List<StayConflict> NewOnBed(AppDbContext db, Occupancy stay, int newBedId, StayRange newRange)
    {
        var after = ForBed(db, newBedId, newRange);
        if (newBedId != stay.BedId) return after;   // a different bed: every overlap there is new

        var before = ForBed(db, stay.BedId, new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate));
        return after
            .Where(a => StayRules.IsNewOrWorse(
                a.Overlap, before.FirstOrDefault(b => b.OccupancyId == a.OccupancyId)?.Overlap))
            .ToList();
    }

    // The same for the tenant's own other stays (one tenant in two beds on the same day).
    public static List<StayConflict> NewForTenant(AppDbContext db, Occupancy stay, StayRange newRange)
    {
        var after = ForTenant(db, stay.TenantId, newRange);
        var before = ForTenant(db, stay.TenantId, new StayRange(stay.Id, stay.MoveInDate, stay.MoveOutDate));
        return after
            .Where(a => StayRules.IsNewOrWorse(
                a.Overlap, before.FirstOrDefault(b => b.OccupancyId == a.OccupancyId)?.Overlap))
            .ToList();
    }

    private static List<StayConflict> Find(IQueryable<Occupancy> candidates, StayRange proposed)
    {
        var stays = candidates
            .Where(o => o.Id != proposed.Id)
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .AsNoTracking()
            .ToList();

        var found = new List<StayConflict>();
        foreach (var o in stays)
        {
            var range = new StayRange(o.Id, o.MoveInDate, o.MoveOutDate);
            if (StayRules.Overlap(proposed, range) is DateSpan span)
                found.Add(new StayConflict(o.Id, o.Tenant.FullName, BedName(o), range, span));
        }
        return found.OrderBy(c => c.Stay.MoveIn).ToList();
    }

    private static string BedName(Occupancy o) => $"{o.Bed.Room.RoomName} / {o.Bed.BedLabel}";

    private static string StayText(StayRange s) =>
        s.MoveOut is DateOnly end
            ? $"{Fmt.Date(s.MoveIn)} to {Fmt.Date(end)}"
            : $"{Fmt.Date(s.MoveIn)}, no move-out date";

    private static string SpanText(DateSpan s) =>
        s.End is DateOnly end
            ? (end == s.Start ? Fmt.Date(s.Start) : $"{Fmt.Date(s.Start)} to {Fmt.Date(end)}")
            : $"{Fmt.Date(s.Start)} onward";

    // Names each conflicting stay and the exact days that overlap.
    public static string Describe(IEnumerable<StayConflict> conflicts) =>
        string.Join("\n", conflicts.Select(c =>
            $"- {c.TenantName}, {c.BedName} ({StayText(c.Stay)}). Overlap: {SpanText(c.Overlap)}"));

    // Read-only: reports existing stays that break the rules. Changes nothing.
    public static List<string> ScanExisting()
    {
        using var db = new AppDbContext();
        var stays = db.Occupancies
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .AsNoTracking()
            .ToList();

        var problems = new List<string>();

        foreach (var s in stays.Where(s => !StayRules.IsValidRange(s.MoveInDate, s.MoveOutDate)))
            problems.Add($"{s.Tenant.FullName}, {BedName(s)}: the move-out date " +
                         $"({Fmt.Date(s.MoveOutDate!.Value)}) is before the move-in date ({Fmt.Date(s.MoveInDate)}).");

        FindPairs(stays.GroupBy(s => s.BedId), problems, sameBed: true);
        FindPairs(stays.GroupBy(s => s.TenantId), problems, sameBed: false);
        return problems;
    }

    private static void FindPairs(IEnumerable<IGrouping<int, Occupancy>> groups, List<string> problems, bool sameBed)
    {
        foreach (var g in groups)
        {
            var list = g.OrderBy(o => o.MoveInDate).ToList();
            for (var i = 0; i < list.Count; i++)
                for (var j = i + 1; j < list.Count; j++)
                {
                    var a = list[i];
                    var b = list[j];
                    var ra = new StayRange(a.Id, a.MoveInDate, a.MoveOutDate);
                    var rb = new StayRange(b.Id, b.MoveInDate, b.MoveOutDate);
                    if (StayRules.Overlap(ra, rb) is not DateSpan span) continue;

                    var handover = span.End == span.Start ? " (same-day handover)" : "";
                    problems.Add(sameBed
                        ? $"{BedName(a)}: {a.Tenant.FullName} ({StayText(ra)}) and {b.Tenant.FullName} ({StayText(rb)}) overlap {SpanText(span)}{handover}."
                        : $"{a.Tenant.FullName}: the stay in {BedName(a)} ({StayText(ra)}) and the stay in {BedName(b)} ({StayText(rb)}) overlap {SpanText(span)}{handover}.");
                }
        }
    }
}