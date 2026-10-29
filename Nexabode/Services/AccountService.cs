using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

public static class AccountRoles
{
    public const string Owner = "Owner";   // can do everything, including managing accounts and restoring a backup
    public const string Staff = "Staff";   // works with tenants, rooms, billing, payments and reports

    // Returns the role spelled the way it is stored, or null if the text is not a role. Capitals don't matter.
    public static string? Normalize(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Equals(Owner, StringComparison.OrdinalIgnoreCase)) return Owner;
        if (t.Equals(Staff, StringComparison.OrdinalIgnoreCase)) return Staff;
        return null;
    }
}

// Adding, removing and changing sign-in accounts. Every method returns an error message, or null when it worked.
// Every change is written to the change history under the name of the person who made it. Passwords never are.
public static class AccountService
{
    private const string DefaultPassword = "admin123";

    public static string? PasswordError(string username, string password)
    {
        if (password.Length < 8) return "The password must be at least 8 characters.";
        if (password == DefaultPassword) return "Please choose a password other than the default.";
        if (password.Equals(username, StringComparison.OrdinalIgnoreCase))
            return "The password can't be the same as the username.";
        return null;
    }

    public static string? NameError(AppDbContext db, string username)
    {
        var name = username.Trim();
        var error = UsernameRules.Validate(name);
        if (error != null) return error;
        if (name.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return "Please choose a username other than the default \"admin\".";

        var lower = name.ToLower();
        return db.AdminUsers.Any(u => u.Username.ToLower() == lower) ? "That username is already taken." : null;
    }

    public static string? Add(AppDbContext db, string username, string role, string temporaryPassword)
    {
        username = username.Trim();

        var nameError = NameError(db, username);
        if (nameError != null) return nameError;

        var canonical = AccountRoles.Normalize(role);
        if (canonical == null) return "The role must be Owner or Staff.";

        var passwordError = PasswordError(username, temporaryPassword);
        if (passwordError != null) return passwordError;

        var user = new AdminUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            Role = canonical,
            MustChangePassword = true   // they choose their own password the first time they sign in
        };
        db.AdminUsers.Add(user);
        db.SaveChanges();

        AuditService.Record(db, "Account", user.Id, null, "Created", null, $"{username} ({canonical})",
                            "Account added in Settings");
        db.SaveChanges();
        return null;
    }

    public static string? ResetPassword(AppDbContext db, int id, string temporaryPassword)
    {
        var user = db.AdminUsers.Find(id);
        if (user == null) return "The account could not be found.";

        var passwordError = PasswordError(user.Username, temporaryPassword);
        if (passwordError != null) return passwordError;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        user.MustChangePassword = true;

        AuditService.Record(db, "Account", user.Id, null, "Password", null, null,
                            $"Password reset for {user.Username}. They choose a new one at their next sign-in.");
        db.SaveChanges();
        return null;
    }

    public static string? SetRole(AppDbContext db, int id, string role)
    {
        var user = db.AdminUsers.Find(id);
        if (user == null) return "The account could not be found.";

        var canonical = AccountRoles.Normalize(role);
        if (canonical == null) return "The role must be Owner or Staff.";
        if (canonical == user.Role) return null;

        if (user.Username.Equals(Session.Administrator, StringComparison.OrdinalIgnoreCase))
            return "You can't change the role of the account you are signed in with.";
        if (user.Role == AccountRoles.Owner && db.AdminUsers.Count(u => u.Role == AccountRoles.Owner) <= 1)
            return "At least one Owner account must remain.";

        AuditService.Record(db, "Account", user.Id, null, "Role", user.Role, canonical,
                            $"Role changed for {user.Username}");
        user.Role = canonical;
        db.SaveChanges();
        return null;
    }

    public static string? Remove(AppDbContext db, int id)
    {
        var user = db.AdminUsers.Find(id);
        if (user == null) return "The account could not be found.";

        if (user.Username.Equals(Session.Administrator, StringComparison.OrdinalIgnoreCase))
            return "You can't remove the account you are signed in with. Sign in with another Owner account to remove it.";
        if (user.Role == AccountRoles.Owner && db.AdminUsers.Count(u => u.Role == AccountRoles.Owner) <= 1)
            return "At least one Owner account must remain.";

        AuditService.Record(db, "Account", user.Id, null, "Removed", $"{user.Username} ({user.Role})", null,
                            "Account removed in Settings");
        db.AdminUsers.Remove(user);
        db.SaveChanges();
        return null;
    }
}