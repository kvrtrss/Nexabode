using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

public class AccountServiceTests
{
    [Fact]
    public void Add_CreatesTheAccount_AndItsHistoryEntry_TogetherWithATemporaryPassword()
    {
        using var test = new TestDb();
        using var db = test.Create();

        Assert.Null(AccountService.Add(db, "maria", AccountRoles.Staff, "Blue-Kettle-77"));

        var user = db.AdminUsers.Single();
        Assert.Equal(AccountRoles.Staff, user.Role);
        Assert.True(user.MustChangePassword);   // a temporary password has to be replaced at the first sign-in
        Assert.Contains(db.AuditLog, a => a.EntityType == "Account" && a.EntityId == user.Id && a.Field == "Created");
    }

    [Fact]
    public void Add_RefusesAWeakTemporaryPassword()
    {
        using var test = new TestDb();
        using var db = test.Create();

        Assert.NotNull(AccountService.Add(db, "maria", AccountRoles.Staff, "12345678"));
        Assert.Empty(db.AdminUsers);
    }

    [Fact]
    public void Add_RefusesAUsernameThatIsAlreadyTaken_IgnoringCapitals()
    {
        using var test = new TestDb();
        using var db = test.Create();
        Assert.Null(AccountService.Add(db, "maria", AccountRoles.Staff, "Blue-Kettle-77"));

        Assert.NotNull(AccountService.Add(db, "MARIA", AccountRoles.Staff, "Green-Lantern-55"));
        Assert.Equal(1, db.AdminUsers.Count());
    }
}
