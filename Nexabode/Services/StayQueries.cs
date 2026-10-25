using Nexabode.Models;

namespace Nexabode.Services;

public static class StayQueries
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    // Stays that hold their bed today or later: ongoing, reserved, or ending today or later.
    public static IQueryable<Occupancy> CurrentOrUpcoming(this IQueryable<Occupancy> stays, DateOnly? asOf = null)
    {
        var day = asOf ?? Today;
        return stays.Where(o => o.MoveOutDate == null || o.MoveOutDate >= day);
    }
}