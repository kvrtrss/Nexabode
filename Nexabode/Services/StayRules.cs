namespace Nexabode.Services;

// A stay as far as the rules are concerned. MoveOut == null means the stay is ongoing.
public readonly record struct StayRange(int Id, DateOnly MoveIn, DateOnly? MoveOut);

// A run of days. End == null means it has no end.
public readonly record struct DateSpan(DateOnly Start, DateOnly? End);

public enum StayPhase { Reserved, Staying, Ended }

public static class StayRules
{
    // The move-out date is the tenant's LAST day in the bed (inclusive).
    public static bool IsValidRange(DateOnly moveIn, DateOnly? moveOut) =>
        moveOut == null || moveOut.Value >= moveIn;

    // The days two stays have in common, or null if they don't overlap.
    public static DateSpan? Overlap(StayRange a, StayRange b)
    {
        var start = a.MoveIn > b.MoveIn ? a.MoveIn : b.MoveIn;
        var aEnd = a.MoveOut ?? DateOnly.MaxValue;
        var bEnd = b.MoveOut ?? DateOnly.MaxValue;
        var end = aEnd < bEnd ? aEnd : bEnd;

        if (start > end) return null;
        return new DateSpan(start, end == DateOnly.MaxValue ? null : end);
    }

    public static bool Overlaps(StayRange a, StayRange b) => Overlap(a, b) != null;

    public static bool IsActiveOn(StayRange s, DateOnly day) =>
        s.MoveIn <= day && (s.MoveOut == null || s.MoveOut.Value >= day);

    // True if the stay still holds its bed on this day or any day after it.
    public static bool BlocksFrom(StayRange s, DateOnly day) =>
        s.MoveOut == null || s.MoveOut.Value >= day;

    public static StayPhase PhaseOn(StayRange s, DateOnly day) =>
        s.MoveIn > day ? StayPhase.Reserved
        : (s.MoveOut == null || s.MoveOut.Value >= day) ? StayPhase.Staying
        : StayPhase.Ended;

    // The first day an open-ended new stay could start without a conflict.
    // Returns null if an ongoing stay means the bed is not free at all.
    public static DateOnly? FreeFrom(IEnumerable<StayRange> stays, DateOnly day)
    {
        var earliest = day;
        foreach (var s in stays)
        {
            if (!BlocksFrom(s, day)) continue;
            if (s.MoveOut == null) return null;
            var next = s.MoveOut.Value.AddDays(1);
            if (next > earliest) earliest = next;
        }
        return earliest;
    }

    // How many days of this stay fall inside the calendar month starting on monthStart (both ends inclusive).
    public static int DaysIn(StayRange s, DateOnly monthStart)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var from = s.MoveIn > monthStart ? s.MoveIn : monthStart;
        var to = s.MoveOut is DateOnly o && o < monthEnd ? o : monthEnd;
        var days = to.DayNumber - from.DayNumber + 1;
        return days > 0 ? days : 0;
    }

    // The days covered by 'a' but not by 'b' (up to two separate runs of days).
    public static List<DateSpan> Subtract(StayRange a, StayRange b)
    {
        var result = new List<DateSpan>();

        if (Overlap(a, b) is not DateSpan common)
        {
            result.Add(new DateSpan(a.MoveIn, a.MoveOut));
            return result;
        }

        if (a.MoveIn < common.Start)
            result.Add(new DateSpan(a.MoveIn, common.Start.AddDays(-1)));

        if (common.End is DateOnly commonEnd && (a.MoveOut == null || a.MoveOut.Value > commonEnd))
            result.Add(new DateSpan(commonEnd.AddDays(1), a.MoveOut));

        return result;
    }

    // True if every day of 'inner' is also a day of 'outer'.
    public static bool IsWithin(DateSpan inner, DateSpan outer)
    {
        if (inner.Start < outer.Start) return false;
        if (outer.End == null) return true;
        return inner.End != null && inner.End.Value <= outer.End.Value;
    }

    // An overlap needs attention only if it is new, or covers days it did not cover before.
    public static bool IsNewOrWorse(DateSpan after, DateSpan? before) =>
        before == null || !IsWithin(after, before.Value);

}