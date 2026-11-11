namespace Nexabode.Services;

// What the sign-in screen remembers about failed attempts.
public readonly record struct LockState(int FailedAttempts, int LockLevel, DateTime? LockedUntilUtc);

// The rules for locking sign-in after too many failed attempts. No database or screens in here,
// so the rules can be tested on their own.
public static class LockoutRules
{
    // 3 failed attempts in a row lock sign-in.
    public const int MaxFailedAttempts = 3;

    // How long each lock lasts. Every new lock in a row is longer, up to the last step.
    // A successful sign-in starts again from the first step.
    private static readonly TimeSpan[] Steps =
    {
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(60)
    };

    public static TimeSpan LongestLock => Steps[^1];

    public static TimeSpan LockDuration(int level) => Steps[Math.Clamp(level, 1, Steps.Length) - 1];

    // Call this on the saved state before using it. An ended lock gives a fresh round of attempts
    // (the lock level is kept, so the next lock is longer). A lock that ends further away than any lock
    // can last (the clock was moved back) is shortened to the longest possible lock.
    public static LockState Refresh(LockState state, DateTime nowUtc)
    {
        if (state.LockedUntilUtc is not DateTime until) return state;

        if (until <= nowUtc) return state with { FailedAttempts = 0, LockedUntilUtc = null };
        if (until - nowUtc > LongestLock) return state with { LockedUntilUtc = nowUtc + LongestLock };
        return state;
    }

    public static bool IsLocked(LockState state, DateTime nowUtc, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (state.LockedUntilUtc is not DateTime until) return false;

        var left = until - nowUtc;
        if (left <= TimeSpan.Zero) return false;

        remaining = left > LongestLock ? LongestLock : left;
        return true;
    }

    public static int AttemptsLeft(LockState state) => Math.Max(0, MaxFailedAttempts - state.FailedAttempts);

    // The state after one more wrong attempt. The third one in a round starts a lock.
    public static LockState AfterFailure(LockState state, DateTime nowUtc)
    {
        var failed = state.FailedAttempts + 1;
        if (failed < MaxFailedAttempts)
            return state with { FailedAttempts = failed };

        var level = Math.Min(state.LockLevel + 1, Steps.Length);
        return new LockState(0, level, nowUtc + LockDuration(level));
    }

    // A correct sign-in clears everything.
    public static LockState AfterSuccess() => new(0, 0, null);

    // "04:32" style text for the countdown.
    public static string Countdown(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
        var totalMinutes = (int)remaining.TotalMinutes;
        return $"{totalMinutes:00}:{remaining.Seconds:00}";
    }
}
