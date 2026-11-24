using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public enum SignInStatus { Success, Failed, Locked, MissingInput }

public sealed class SignInResult
{
    public SignInStatus Status { get; init; }
    public AdminUser? User { get; init; }          // set only when Status is Success
    public int AttemptsLeft { get; init; }         // set when Status is Failed
    public TimeSpan LockRemaining { get; init; }   // set when Status is Locked
}

public sealed class EmergencyResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool Locked { get; init; }
    public string? Username { get; init; }          // the Owner account that was created or whose password was reset
    public bool WasReset { get; init; }             // true when an existing Owner's password was replaced
    public string? NewEmergencyKey { get; init; }   // the replacement key, shown once
}

// Signing in, the lock after failed attempts, and the Owner recovery key: the one way back in when an Owner
// forgets their password. (Staff who forget theirs ask an Owner to reset it in Settings.)
// What this class writes to the change history never includes a password, a recovery code,
// or what was typed in the username box (people sometimes type their password there by mistake).
public static class AuthService
{
    // Checked when the account doesn't exist, so a wrong username takes as long as a wrong password
    // and the sign-in screen can't be used to find out which usernames exist.
    private static readonly Lazy<string> DummyHash =
        new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")));

    private const string SignInAudit = "(sign-in screen)";

    // ---------- the lock ----------

    private static SignInGuard LoadGuard(AppDbContext db)
    {
        var guard = db.SignInGuards.FirstOrDefault();
        if (guard == null)
        {
            guard = new SignInGuard();
            db.SignInGuards.Add(guard);
        }
        return guard;
    }

    private static LockState Read(SignInGuard guard, DateTime nowUtc) =>
        LockoutRules.Refresh(new LockState(guard.FailedAttempts, guard.LockLevel,
            guard.LockedUntilUtc == null ? null : DateTime.SpecifyKind(guard.LockedUntilUtc.Value, DateTimeKind.Utc)),
            nowUtc);

    private static void Write(SignInGuard guard, LockState state)
    {
        guard.FailedAttempts = state.FailedAttempts;
        guard.LockLevel = state.LockLevel;
        guard.LockedUntilUtc = state.LockedUntilUtc;
    }

    // True while sign-in is locked. The lock is kept in the database, so closing and reopening
    // Nexabode (or logging out) does not clear it.
    public static bool IsLocked(out TimeSpan remaining)
    {
        using var db = AppDb.Create();
        var guard = db.SignInGuards.FirstOrDefault();
        if (guard == null)
        {
            remaining = TimeSpan.Zero;
            return false;
        }
        return LockoutRules.IsLocked(Read(guard, Clock.UtcNow), Clock.UtcNow, out remaining);
    }

    // Counts one wrong attempt and saves it. Returns the new state. 'what' is for the change history.
    private static LockState RegisterFailure(AppDbContext db, SignInGuard guard, LockState state,
                                             DateTime nowUtc, string what)
    {
        var next = LockoutRules.AfterFailure(state, nowUtc);
        Write(guard, next);

        if (next.LockedUntilUtc != null)
        {
            var minutes = (int)LockoutRules.LockDuration(next.LockLevel).TotalMinutes;
            AuditService.Record(db, "Security", 0, null, "Sign-in lock", null, null,
                $"{what} locked for {minutes} minutes after {LockoutRules.MaxFailedAttempts} failed attempts",
                administrator: SignInAudit);
        }

        db.SaveChanges();
        return next;
    }

    // ---------- signing in ----------

    public static SignInResult SignIn(string? username, string? password)
    {
        username = (username ?? "").Trim();
        password ??= "";

        // An empty form is not a guess, so it doesn't count as a failed attempt.
        if (username.Length == 0 || password.Length == 0)
            return new SignInResult { Status = SignInStatus.MissingInput };

        using var db = AppDb.Create();
        var now = Clock.UtcNow;
        var guard = LoadGuard(db);
        var state = Read(guard, now);

        // While locked, nothing is checked at all: not even a correct password gets in.
        if (LockoutRules.IsLocked(state, now, out var left))
            return new SignInResult { Status = SignInStatus.Locked, LockRemaining = left };

        var lower = username.ToLower();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);

        var hashMatches = BCrypt.Net.BCrypt.Verify(password, user?.PasswordHash ?? DummyHash.Value);
        if (user != null && hashMatches)
        {
            Write(guard, LockoutRules.AfterSuccess());
            db.SaveChanges();
            return new SignInResult { Status = SignInStatus.Success, User = user };
        }

        var next = RegisterFailure(db, guard, state, now, "Sign-in");
        return LockoutRules.IsLocked(next, now, out var lockedFor)
            ? new SignInResult { Status = SignInStatus.Locked, LockRemaining = lockedFor }
            : new SignInResult { Status = SignInStatus.Failed, AttemptsLeft = LockoutRules.AttemptsLeft(next) };
    }

    // Only checks a password, for a person who is already signed in (it does not touch the lock).
    public static bool VerifyPassword(string username, string password)
    {
        using var db = AppDb.Create();
        var lower = (username ?? "").Trim().ToLower();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);
        var matches = BCrypt.Net.BCrypt.Verify(password ?? "", user?.PasswordHash ?? DummyHash.Value);
        return user != null && matches;
    }

    // ---------- emergency access ----------

    private const string EmergencyAudit = "(emergency access)";

    public static bool HasEmergencyKey()
    {
        using var db = AppDb.Create();
        return db.SignInGuards.Select(g => g.EmergencyKeyHash).FirstOrDefault() != null;
    }

    // Saves an emergency access key, replacing any earlier one (which stops working).
    public static void SaveEmergencyKey(string key, string reason)
    {
        using var db = AppDb.Create();
        var guard = LoadGuard(db);
        guard.EmergencyKeyHash = BCrypt.Net.BCrypt.HashPassword(RecoveryCodeRules.Normalize(key));
        AuditService.Record(db, "Security", 0, null, "Recovery key", null, null, reason,
                            administrator: Session.Administrator);
        db.SaveChanges();
    }

    // ---------- the Owner recovery key ----------

    private const string SetupAudit = "(first-run setup)";

    // True once at least one account exists. A brand-new install has none, so setup comes first.
    public static bool HasAnyAccount()
    {
        using var db = AppDb.Create();
        return db.AdminUsers.Any();
    }

    // First run only: creates the first Owner account and saves the Owner recovery key together.
    // 'recoveryKey' is what RecoveryCodeRules.Generate returned and has already been shown to the person;
    // only its hash is kept. Returns an error message, or null when it worked.
    public static string? CreateFirstOwner(string username, string password, string recoveryKey)
    {
        username = (username ?? "").Trim();

        using var db = AppDb.Create();
        if (db.AdminUsers.Any()) return "An account already exists. Sign in with it instead.";

        var nameError = AccountService.NameError(db, username);
        if (nameError != null) return nameError;

        var passwordError = AccountService.PasswordError(username, password);
        if (passwordError != null) return passwordError;

        var guard = LoadGuard(db);

        // The account, the key and the history entries are saved as one unit: all of it, or none of it.
        using var transaction = db.Database.BeginTransaction();

        var user = new AdminUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = AccountRoles.Owner,
            MustChangePassword = false   // they just chose their own password
        };
        db.AdminUsers.Add(user);
        db.SaveChanges();   // gives the account its id for the history entry

        guard.EmergencyKeyHash = BCrypt.Net.BCrypt.HashPassword(RecoveryCodeRules.Normalize(recoveryKey));

        AuditService.Record(db, "Account", user.Id, null, "Created", null, $"{username} ({AccountRoles.Owner})",
                            "First Owner account created at setup", administrator: SetupAudit);
        AuditService.Record(db, "Security", 0, null, "Recovery key", null, null,
                            "First Owner recovery key made at setup", administrator: SetupAudit);
        db.SaveChanges();
        transaction.Commit();
        return null;
    }

    // For an Owner who forgot their password (or when nobody can sign in). With the right Owner recovery key:
    //  - if an Owner account has the typed username, its password is replaced;
    //  - if no account has that username, a new Owner account is created.
    // A Staff account is never touched: an Owner resets those in Settings.
    // The key works once: using it makes a new one. Wrong keys count toward the same lock as sign-in.
    public static EmergencyResult RecoverAccess(string? key, string? username, string? password, string? confirm)
    {
        username = (username ?? "").Trim();
        password ??= "";
        var normalized = RecoveryCodeRules.Normalize(key);

        using var db = AppDb.Create();
        var now = Clock.UtcNow;
        var guard = LoadGuard(db);
        var state = Read(guard, now);

        if (LockoutRules.IsLocked(state, now, out _))
            return new EmergencyResult { Locked = true, Error = "Too many unsuccessful attempts. Please wait until the lock ends." };

        if (normalized.Length == 0 || username.Length == 0)
            return new EmergencyResult { Error = "Enter the Owner recovery key and your username." };

        // The password rules are public, so they are checked first and a typo here doesn't use up an attempt.
        var passwordError = AccountService.PasswordError(username, password);
        if (passwordError != null) return new EmergencyResult { Error = passwordError };
        if (password != confirm) return new EmergencyResult { Error = "The new passwords do not match." };

        var stored = guard.EmergencyKeyHash;
        var matches = BCrypt.Net.BCrypt.Verify(normalized, stored ?? DummyHash.Value);
        if (stored == null || !matches)
        {
            var next = RegisterFailure(db, guard, state, now, "Owner recovery key");
            var locked = LockoutRules.IsLocked(next, now, out _);
            var left = LockoutRules.AttemptsLeft(next);
            return new EmergencyResult
            {
                Locked = locked,
                Error = locked
                    ? "Too many unsuccessful attempts. For your security, sign-in is now temporarily locked."
                    : $"The Owner recovery key is not correct. {left} attempt{(left == 1 ? "" : "s")} remaining before sign-in is locked."
            };
        }

        // The key is right, so it is safe to say what can and can't be done with the username.
        var lower = username.ToLower();
        var existing = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);

        if (existing != null && existing.Role != AccountRoles.Owner)
            return new EmergencyResult
            {
                Error = "That is a Staff account. Ask an Owner to reset its password in Settings, " +
                        "or choose a different username to create a new Owner account."
            };

        if (existing == null)
        {
            var nameError = AccountService.NameError(db, username);
            if (nameError != null) return new EmergencyResult { Error = nameError };
        }

        var newKey = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);

        // Everything below is saved as one unit: the account change, the replaced key, the cleared lock
        // and the history entries. If anything fails in between, none of it is kept.
        using var transaction = db.Database.BeginTransaction();

        AdminUser user;
        if (existing != null)
        {
            user = existing;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.MustChangePassword = false;   // they just chose their own password
        }
        else
        {
            user = new AdminUser
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = AccountRoles.Owner,
                MustChangePassword = false
            };
            db.AdminUsers.Add(user);
            db.SaveChanges();   // gives the account its id for the history entry
        }

        guard.EmergencyKeyHash = BCrypt.Net.BCrypt.HashPassword(newKey);
        Write(guard, LockoutRules.AfterSuccess());

        if (existing != null)
            AuditService.Record(db, "Account", user.Id, null, "Password", null, null,
                "Owner password reset with the Owner recovery key", administrator: EmergencyAudit);
        else
            AuditService.Record(db, "Account", user.Id, null, "Created", null, $"{user.Username} ({AccountRoles.Owner})",
                "Owner account created with the Owner recovery key", administrator: EmergencyAudit);

        AuditService.Record(db, "Security", 0, null, "Recovery key", null, null,
            "Owner recovery key used, and replaced with a new one", administrator: EmergencyAudit);

        db.SaveChanges();
        transaction.Commit();

        return new EmergencyResult
        {
            Success = true,
            Username = user.Username,
            WasReset = existing != null,
            NewEmergencyKey = RecoveryCodeRules.Format(newKey)
        };
    }
}
