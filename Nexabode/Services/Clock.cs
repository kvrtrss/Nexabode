namespace Nexabode.Services;

// The one place that knows what time it is. The app code asks the Clock instead of the computer directly,
// so a test can freeze time and check things like "overdue" or "locked for ten minutes" on a fixed day.
public static class Clock
{
    private static Func<DateTime>? _fixed;

    public static DateTime Now => _fixed?.Invoke() ?? DateTime.Now;
    public static DateTime Today => Now.Date;
    public static DateTime UtcNow => _fixed == null
        ? DateTime.UtcNow
        : DateTime.SpecifyKind(_fixed(), DateTimeKind.Local).ToUniversalTime();

    // For tests only.
    public static void Freeze(DateTime now) => _fixed = () => now;
    public static void Reset() => _fixed = null;
}
