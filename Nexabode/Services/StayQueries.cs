using Nexabode.Models;

namespace Nexabode.Services;

public static class StayQueries
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    // Stays that hold their bed today or later: ongoing, reserved, or ending today or later.
    // A stay whose tenant has been confirmed as moved out no longer holds the bed (see StayRules.LastDayHoldingBed),
    // so a bed is available as soon as its tenant has left, not the day after.
    public static IQueryable<Occupancy> CurrentOrUpcoming(this IQueryable<Occupancy> stays, DateOnly? asOf = null)
    {
        var day = asOf ?? Today;
        return stays.Where(o => (o.MoveOutDate == null || o.MoveOutDate >= day)
                                && (o.MovedOutOn == null || o.MovedOutOn > day));
    }

    // The days a stay holds its bed, for checking that two stays don't clash on the same bed.
    public static StayRange BedRange(this Occupancy o) =>
        new(o.Id, o.MoveInDate, StayRules.LastDayHoldingBed(o.MoveOutDate, o.MovedOutOn));
}