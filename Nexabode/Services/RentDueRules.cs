namespace Nexabode.Services;

// When a rent charge falls due. Kept free of screens and database code, so it can be tested on its own.
public static class RentDueRules
{
    public const int DefaultDueDay = 5;

    // The usual due date in a month: the chosen day of the month, or the last day when the month is shorter.
    public static DateOnly UsualDueDate(DateOnly monthStart, int dueDay)
    {
        var day = Math.Clamp(dueDay, 1, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
        return new DateOnly(monthStart.Year, monthStart.Month, day);
    }

    // The usual due date for the month, but never before the stay starts. Without this, someone who moves in
    // on the 13th would have rent that was "due" on the 5th, and would show as overdue before staying a single night.
    public static DateOnly DueDate(DateOnly usualDue, DateOnly moveIn) => moveIn > usualDue ? moveIn : usualDue;
}
