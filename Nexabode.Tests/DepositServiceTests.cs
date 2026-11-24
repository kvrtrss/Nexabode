using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

public class DepositServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    // A tenant holding a deposit, with two unpaid charges: 300 due in January and 400 due in February.
    private static int TenantWithDepositAndTwoCharges(TestDb test, decimal deposit)
    {
        using var db = test.Create();
        var (tenant, stay) = Seed.TenantWithStay(db, deposit: deposit);
        Seed.AddCharge(db, stay, 300m, new DateOnly(2026, 1, 5), "2026-01");
        Seed.AddCharge(db, stay, 400m, new DateOnly(2026, 2, 5), "2026-02");
        return tenant.Id;
    }

    [Fact]
    public void Apply_PaysTheOldestChargeFirst()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 1000m);

        using (var db = test.Create())
            Assert.Null(DepositService.Apply(db, tenantId, 500m, Today, null));

        using (var db = test.Create())
        {
            var charges = db.Charges.Include(c => c.Allocations).OrderBy(c => c.DueDate).ToList();
            Assert.Equal(300m, charges[0].Allocations.Sum(a => a.AmountApplied));   // the January charge is cleared first
            Assert.Equal(200m, charges[1].Allocations.Sum(a => a.AmountApplied));   // the rest goes to February
            Assert.Equal(500m, DepositService.Summary(db, tenantId).Held);
        }
    }

    [Fact]
    public void Apply_RefusesMoreThanTheDepositStillHeld()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 100m);

        using var db = test.Create();
        var error = DepositService.Apply(db, tenantId, 200m, Today, null);

        Assert.NotNull(error);
        Assert.Contains("deposit still held", error);
        Assert.Equal(0, db.Payments.Count());
    }

    [Fact]
    public void Apply_RefusesMoreThanTheTenantOwes()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 5000m);   // owes only 700 in total

        using var db = test.Create();
        var error = DepositService.Apply(db, tenantId, 800m, Today, null);

        Assert.NotNull(error);
        Assert.Equal(0, db.Payments.Count());
    }

    [Fact]
    public void Refund_ReducesTheDepositHeld()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 1000m);

        using (var db = test.Create())
            Assert.Null(DepositService.Refund(db, tenantId, 400m, Today, null));

        using (var db = test.Create())
            Assert.Equal(600m, DepositService.Summary(db, tenantId).Held);
    }

    [Fact]
    public void Refund_RefusesMoreThanTheDepositStillHeld()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 1000m);

        using var db = test.Create();
        Assert.NotNull(DepositService.Refund(db, tenantId, 1500m, Today, null));
        Assert.Equal(1000m, DepositService.Summary(db, tenantId).Held);
    }

    [Fact]
    public void VoidingAPaymentMadeFromTheDeposit_GivesTheMoneyBack()
    {
        using var test = new TestDb();
        var tenantId = TenantWithDepositAndTwoCharges(test, deposit: 1000m);

        using (var db = test.Create())
            Assert.Null(DepositService.Apply(db, tenantId, 300m, Today, null));

        using (var db = test.Create())
        {
            Assert.Equal(700m, DepositService.Summary(db, tenantId).Held);

            var payment = db.Payments.Single();
            payment.IsVoided = true;
            db.SaveChanges();
        }

        using (var db = test.Create())
            Assert.Equal(1000m, DepositService.Summary(db, tenantId).Held);
    }
}
