using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

public class AuthServiceTests
{
    private const string Password = "Sunflower-Lamp-42";

    private static string NewKey() => RecoveryCodeRules.Generate(RecoveryCodeRules.EmergencyKeyLength);

    // An Owner called kurt, with the recovery key returned so the test can use it.
    private static string FirstOwner()
    {
        var key = NewKey();
        Assert.Null(AuthService.CreateFirstOwner("kurt", Password, key));
        return key;
    }

    [Fact]
    public void CreateFirstOwner_MakesAnOwner_AndOnlyOnce()
    {
        using var test = new TestDb();

        FirstOwner();

        using var db = test.Create();
        var user = db.AdminUsers.Single();
        Assert.Equal("kurt", user.Username);
        Assert.Equal(AccountRoles.Owner, user.Role);
        Assert.False(user.MustChangePassword);
        Assert.True(AuthService.HasEmergencyKey());

        // once an account exists, setup is closed
        Assert.NotNull(AuthService.CreateFirstOwner("someone", Password, NewKey()));
    }

    [Fact]
    public void CreateFirstOwner_RefusesAWeakPassword()
    {
        using var test = new TestDb();

        Assert.NotNull(AuthService.CreateFirstOwner("kurt", "12345678", NewKey()));
        Assert.False(AuthService.HasAnyAccount());
    }

    [Fact]
    public void CreateFirstOwner_WritesTheAccountAndKeyIntoTheHistory()
    {
        using var test = new TestDb();
        FirstOwner();

        using var db = test.Create();
        Assert.Contains(db.AuditLog, a => a.EntityType == "Account" && a.Field == "Created");
        Assert.Contains(db.AuditLog, a => a.EntityType == "Security" && a.Field == "Recovery key");
    }

    [Fact]
    public void SignIn_WithTheRightPassword_Succeeds()
    {
        using var test = new TestDb();
        FirstOwner();

        var result = AuthService.SignIn("kurt", Password);

        Assert.Equal(SignInStatus.Success, result.Status);
        Assert.Equal("kurt", result.User!.Username);
    }

    [Fact]
    public void SignIn_ByUsername_IgnoresCapitals()
    {
        using var test = new TestDb();
        FirstOwner();

        Assert.Equal(SignInStatus.Success, AuthService.SignIn("KURT", Password).Status);
    }

    [Fact]
    public void SignIn_WithAWrongPassword_FailsAndSaysHowManyAttemptsAreLeft()
    {
        using var test = new TestDb();
        FirstOwner();

        var result = AuthService.SignIn("kurt", "not-the-password");

        Assert.Equal(SignInStatus.Failed, result.Status);
        Assert.True(result.AttemptsLeft > 0);
    }

    [Fact]
    public void ThreeWrongPasswords_LockSignIn_EvenForTheRightPassword_UntilTheLockEnds()
    {
        using var test = new TestDb();
        FirstOwner();

        for (var i = 0; i < 3; i++)
            AuthService.SignIn("kurt", "wrong-password-" + i);

        Assert.Equal(SignInStatus.Locked, AuthService.SignIn("kurt", Password).Status);

        // a day later the lock is over
        Clock.Freeze(new DateTime(2026, 11, 14, 10, 0, 0));
        Assert.Equal(SignInStatus.Success, AuthService.SignIn("kurt", Password).Status);
    }

    [Fact]
    public void RecoverAccess_WithTheRightKey_ResetsAnOwnersPassword()
    {
        using var test = new TestDb();
        var key = FirstOwner();

        var result = AuthService.RecoverAccess(key, "kurt", "Brand-New-Pass-77", "Brand-New-Pass-77");

        Assert.True(result.Success);
        Assert.True(result.WasReset);
        Assert.Equal(SignInStatus.Success, AuthService.SignIn("kurt", "Brand-New-Pass-77").Status);
        Assert.Equal(SignInStatus.Failed, AuthService.SignIn("kurt", Password).Status);
    }

    [Fact]
    public void TheRecoveryKey_WorksOnce_ThenIsReplaced()
    {
        using var test = new TestDb();
        var key = FirstOwner();

        var first = AuthService.RecoverAccess(key, "kurt", "Brand-New-Pass-77", "Brand-New-Pass-77");
        Assert.True(first.Success);
        Assert.False(string.IsNullOrEmpty(first.NewEmergencyKey));

        var again = AuthService.RecoverAccess(key, "kurt", "Another-New-Pass-88", "Another-New-Pass-88");
        Assert.False(again.Success);   // the old key no longer works

        // but the new key does
        var withNew = AuthService.RecoverAccess(first.NewEmergencyKey, "kurt", "Another-New-Pass-88", "Another-New-Pass-88");
        Assert.True(withNew.Success);
    }

    [Fact]
    public void RecoverAccess_WithAWrongKey_FailsAndChangesNothing()
    {
        using var test = new TestDb();
        FirstOwner();

        var result = AuthService.RecoverAccess(NewKey(), "kurt", "Brand-New-Pass-77", "Brand-New-Pass-77");

        Assert.False(result.Success);
        Assert.Equal(SignInStatus.Success, AuthService.SignIn("kurt", Password).Status);
    }

    [Fact]
    public void RecoverAccess_WithANewUsername_CreatesAnotherOwner()
    {
        using var test = new TestDb();
        var key = FirstOwner();

        var result = AuthService.RecoverAccess(key, "second", "Brand-New-Pass-77", "Brand-New-Pass-77");

        Assert.True(result.Success);
        Assert.False(result.WasReset);

        using var db = test.Create();
        Assert.Equal(2, db.AdminUsers.Count());
        Assert.Equal(AccountRoles.Owner, db.AdminUsers.Single(u => u.Username == "second").Role);
    }

    [Fact]
    public void RecoverAccess_NeverTouchesAStaffAccount()
    {
        using var test = new TestDb();
        var key = FirstOwner();
        using (var db = test.Create())
            Assert.Null(AccountService.Add(db, "maria", AccountRoles.Staff, "Blue-Kettle-77"));

        var result = AuthService.RecoverAccess(key, "maria", "Brand-New-Pass-77", "Brand-New-Pass-77");

        Assert.False(result.Success);
        Assert.Contains("Staff", result.Error);
    }
}
