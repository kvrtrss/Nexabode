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
    public string? Username { get; init; }          // the Owner account that was created
    public string? NewEmergencyKey { get; init; }   // the replacement key, shown once
}

public sealed class RecoveryResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool Locked { get; init; }              // sign-in is now locked (or already was)
    public string? NewRecoveryCode { get; init; }  // the replacement code, shown once; set only on success
}

// Signing in, the lock after failed attempts, choosing a new password with a recovery code,
// and the emergency access key for when no account can be signed in to.
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
        using var db = new AppDbContext();
        var guard = db.SignInGuards.FirstOrDefault();
        if (guard == null)
        {
            remaining = TimeSpan.Zero;
            return false;
        }
        return LockoutRules.IsLocked(Read(guard, DateTime.UtcNow), DateTime.UtcNow, out remaining);
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

        using var db = new AppDbContext();
        var now = DateTime.UtcNow;
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
        using var db = new AppDbContext();
        var lower = (username ?? "").Trim().ToLower();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);
        var matches = BCrypt.Net.BCrypt.Verify(password ?? "", user?.PasswordHash ?? DummyHash.Value);
        return user != null && matches;
    }

    // ---------- forgot password ----------

    // Chooses a new password for the account when the recovery code is right. Wrong guesses count
    // toward the same lock as sign-in, so the code can't be tried over and over.
    // The error for a wrong username and for a wrong code is the same, so it doesn't reveal which usernames exist.
    public static RecoveryResult Recover(string? username, string? code, string? newPassword, string? confirmPassword)
    {
        username = (username ?? "").Trim();
        newPassword ??= "";
        var normalized = RecoveryCodeRules.Normalize(code);

        using var db = new AppDbContext();
        var now = DateTime.UtcNow;
        var guard = LoadGuard(db);
        var state = Read(guard, now);

        if (LockoutRules.IsLocked(state, now, out _))
            return new RecoveryResult { Locked = true, Error = "Too many unsuccessful attempts. Please wait until the lock ends." };

        if (username.Length == 0 || normalized.Length == 0)
            return new RecoveryResult { Error = "Enter your username and recovery code." };

        // The password rules are public, so they are checked first and a typo here doesn't use up an attempt.
        var passwordError = AccountService.PasswordError(username, newPassword);
        if (passwordError != null) return new RecoveryResult { Error = passwordError };
        if (newPassword != confirmPassword) return new RecoveryResult { Error = "The new passwords do not match." };

        var lower = username.ToLower();
        var user = db.AdminUsers.FirstOrDefault(u => u.Username.ToLower() == lower);

        var hashMatches = BCrypt.Net.BCrypt.Verify(normalized, user?.RecoveryCodeHash ?? DummyHash.Value);
        if (user == null || user.RecoveryCodeHash == null || !hashMatches)
        {
            var next = RegisterFailure(db, guard, state, now, "Password recovery");
            var locked = LockoutRules.IsLocked(next, now, out _);
            var left = LockoutRules.AttemptsLeft(next);
            return new RecoveryResult
            {
                Locked = locked,
                Error = locked
                    ? "Too many unsuccessful attempts. For your security, sign-in is now temporarily locked."
                    : $"The username and recovery code do not match. {left} attempt{(left == 1 ? "" : "s")} remaining before sign-in is locked."
            };
        }

        // The recovery code works once: choosing a new password gives a new code.
        var newCode = RecoveryCodeRules.Generate();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;   // they just chose their own password
        user.RecoveryCodeHash = BCrypt.Net.BCrypt.HashPassword(newCode);

        AuditService.Record(db, "Account", user.Id, null, "Password", null, null,
            "Password changed with a recovery code (a new recovery code was created)", administrator: user.Username);
        Write(guard, LockoutRules.AfterSuccess());
        db.SaveChanges();   // the password, the new code, the history entry and the cleared lock are saved together

        return new RecoveryResult { Success = true, NewRecoveryCode = RecoveryCodeRules.Format(newCode) };
    }

    // ---------- recovery codes ----------

    // Saves a recovery code for the account, replacing any earlier one (which stops working).
    // 'code' is what RecoveryCodeRules.Generate returned; only its hash is kept.
    public static void SaveRecoveryCode(int userId, string code, string reason)
    {
        using var db = new AppDbContext();
        var user = db.AdminUsers.Find(userId) ?? throw new InvalidOperationException("The account could not be found.");

        user.RecoveryCodeHash = BCrypt.Net.BCrypt.HashPassword(RecoveryCodeRules.Normalize(code));
        AuditService.Record(db, "Account", user.Id, null, "Recovery code", null, null, reason,
                            administrator: user.Username);
        db.SaveChanges();
    }

    // True when the account already has a recovery code.
    public static bool HasRecoveryCode(int userId)
    {
        using var db = new AppDbContext();
        return db.AdminUsers.Where(u => u.Id == userId).Select(u => u.RecoveryCodeHash).FirstOrDefault() != null;
    }

    // ---------- emergency access ----------

    private const string EmergencyAudit = "(emergency access)";

    public static bool HasEmergencyKey()
    {
        using var db = new AppDbContext();
        return db.SignInGuards.Select(g => g.EmergencyKeyHash).FirstOrDefault() != null;
    }

    // Saves an emergency access key, replacing any earlier one (which stops working).
    public static void SaveEmergencyKey(string key, string reason)
    {
        using var db = new AppDbContext();
        var guard = LoadGuard(db);
        guard.EmergencyKeyHash = BCrypt.Net.BCrypt.HashPassword(RecoveryCodeRules.Normalize(key));
        AuditService.Record(db, "Security", 0, null, "Emergency key", null, null, reason,
                            administrator: Session.Administrator);
        db.SaveChanges();
    }

    // ---------- reset phrase ----------

    private const string PhraseAudit = "(reset phrase)";

    public static bool HasResetPhrase()
    {
        using var db = new AppDbContext();
        return db.SignInGuards.Select(g => g.ResetPhraseHash).FirstOrDefault() != null;
    }

    // The username of an account, for printing on its recovery sheet.
    public static string? UsernameOf(int userId)
    {
        using var db = new AppDbContext();
        return db.AdminUsers.Where(u => u.Id == userId).Select(u => u.Username).FirstOrDefault();
    }

    // Saves the reset phrase, replacing any earlier one. The rules are checked by the caller
    // (ResetPhraseRules.Error); only the hash is kept, and the phrase never reaches the change history.
    public static void SaveResetPhrase(string phrase, string reason)
    {
        using var db = new AppDbContext();
        var guard = LoadGuard(db);
        guard.ResetPhraseHash = BCrypt.Net.BCrypt.HashPassword(ResetPhraseRules.Normalize(phrase));
        AuditService.Record(db, "Security", 0, null, "Reset phrase", null, null, reason,
                            administrator: Session.Administrator);
        db.SaveChanges();
    }

    public static void ClearResetPhrase(string reason)
    {
        using var db = new AppDbContext();
        var guard = LoadGuard(db);
        guard.ResetPhraseHash = null;
        AuditService.Record(db, "Security", 0, null, "Reset phrase", null, null, reason,
                            administrator: Session.Administrator);
        db.SaveChanges();
    }

    // ---------- getting back in when nobody can sign in ----------

    private enum AccessSecret { EmergencyKey, ResetPhrase }

    // For when nobody can sign in. With the right emergency key, makes a NEW Owner account. It does not
    // change or remove any existing account. The key can only be used once: using it makes a new one.
    // Wrong keys count toward the same lock as sign-in, so the key can't be tried over and over.
    public static EmergencyResult EmergencyAccess(string? key, string? username, string? password, string? confirm) =>
        Access(AccessSecret.EmergencyKey, key, username, password, confirm);

    // The same, with the Owner's reset phrase instead of the emergency key. The phrase can only be used once too:
    // using it removes it, and an Owner sets a new one in Settings.
    public static EmergencyResult ResetPhraseAccess(string? phrase, string? username, string? password, string? confirm) =>
        Access(AccessSecret.ResetPhrase, phrase, username, password, confirm);

    private static EmergencyResult Access(AccessSecret secret, string? typed, string? username, string? password, string? confirm)
    {
        var isKey = secret == AccessSecret.EmergencyKey;
        var label = isKey ? "emergency access key" : "reset phrase";

        username = (username ?? "").Trim();
        password ??= "";
        var normalized = isKey ? RecoveryCodeRules.Normalize(typed) : ResetPhraseRules.Normalize(typed);

        using var db = new AppDbContext();
        var now = DateTime.UtcNow;
        var guard = LoadGuard(db);
        var state = Read(guard, now);

        if (LockoutRules.IsLocked(state, now, out _))
            return new EmergencyResult { Locked = true, Error = "Too many unsuccessful attempts. Please wait until the lock ends." };

        if (normalized.Length == 0 || username.Length == 0)
            return new EmergencyResult { Error = $"Enter the {label} and a username for the new Owner account." };

        // The password rules are public, so they are checked first and a typo here doesn't use up an attempt.
        var passwordError = AccountService.PasswordError(username, password);
        if (passwordError != null) return new EmergencyResult { Error = passwordError };
        if (password != confirm) return new EmergencyResult { Error = "The new passwords do not match." };

        var stored = isKey ? guard.EmergencyKeyHash : guard.ResetPhraseHash;
        var matches = BCrypt.Net.BCrypt.Verify(normalized, stored ?? DummyHash.Value);
        if (stored == null || !matches)
        {
            var next = RegisterFailure(db, guard, state, now, isKey ? "Emergency access" : "Reset phrase access");
            var locked = LockoutRules.IsLocked(next, now, out _);
            var left = LockoutRules.AttemptsLeft(next);
            return new EmergencyResult
            {
                Locked = locked,
                Error = locked
                    ? "Too many unsuccessful attempts. For your security, sign-in is now temporarily locked."
                    : $"The {label} is not correct. {left} attempt{(left == 1 ? "" : "s")} remaining before sign-in is locked."
            };
        }

        // The secret is right, so it is safe to say whether the username can be used.
        var nameError = AccountService.NameError(db, username);
        if (nameError != null) return new EmergencyResult { Error = nameError };

        var user = new AdminUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = AccountRoles.Owner,
            MustChangePassword = false   // they just chose their own password
        };
        db.AdminUsers.Add(user);
        db.SaveChanges();   // gives the account its id for the history entry

        string? newKey = null;
        if (isKey)
        {
            newKey = RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);
            guard.EmergencyKeyHash = BCrypt.Net.BCrypt.HashPassword(newKey);
        }
        else
        {
            guard.ResetPhraseHash = null;   // used once, then gone
        }
        Write(guard, LockoutRules.AfterSuccess());

        var audit = isKey ? EmergencyAudit : PhraseAudit;
        AuditService.Record(db, "Account", user.Id, null, "Created", null, $"{username} ({AccountRoles.Owner})",
            $"Owner account created with the {label}", administrator: audit);
        AuditService.Record(db, "Security", 0, null, isKey ? "Emergency key" : "Reset phrase", null, null,
            isKey ? "Emergency access key used, and replaced with a new one"
                  : "Reset phrase used and removed. An Owner can set a new one in Settings",
            administrator: audit);
        db.SaveChanges();   // the new key, the history entries and the cleared lock are saved together

        return new EmergencyResult
        {
            Success = true,
            Username = username,
            NewEmergencyKey = newKey is null ? null : RecoveryCodeRules.Format(newKey)
        };
    }
}
