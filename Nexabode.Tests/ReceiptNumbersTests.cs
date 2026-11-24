using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

public class ReceiptNumbersTests
{
    private static Payment PaymentOn(int tenantId, DateOnly date) =>
        new() { TenantId = tenantId, PaymentDate = date, Amount = 100m, Method = "Cash" };

    [Fact]
    public void Receipts_AreNumberedInDateOrder_AndRestartEachYear()
    {
        using var test = new TestDb();
        using var db = test.Create();
        var (tenant, _) = Seed.TenantWithStay(db);

        var march = PaymentOn(tenant.Id, new DateOnly(2026, 3, 1));
        var february = PaymentOn(tenant.Id, new DateOnly(2026, 2, 1));
        var nextYear = PaymentOn(tenant.Id, new DateOnly(2027, 1, 1));
        db.Payments.AddRange(march, february, nextYear);
        db.SaveChanges();

        Assert.Equal(ReceiptNumberRules.Format(2026, 1), february.ReceiptNo);
        Assert.Equal(ReceiptNumberRules.Format(2026, 2), march.ReceiptNo);
        Assert.Equal(ReceiptNumberRules.Format(2027, 1), nextYear.ReceiptNo);
    }

    [Fact]
    public void ANewPayment_ContinuesAfterTheLastNumberOfItsYear()
    {
        using var test = new TestDb();
        using var db = test.Create();
        var (tenant, _) = Seed.TenantWithStay(db);

        db.Payments.AddRange(PaymentOn(tenant.Id, new DateOnly(2026, 1, 5)), PaymentOn(tenant.Id, new DateOnly(2026, 1, 6)));
        db.SaveChanges();

        var later = PaymentOn(tenant.Id, new DateOnly(2026, 6, 1));
        db.Payments.Add(later);
        db.SaveChanges();

        Assert.Equal(ReceiptNumberRules.Format(2026, 3), later.ReceiptNo);
    }

    [Fact]
    public void Peek_ShowsTheNumberTheNextPaymentWillGet()
    {
        using var test = new TestDb();
        using var db = test.Create();
        var (tenant, _) = Seed.TenantWithStay(db);
        db.Payments.Add(PaymentOn(tenant.Id, new DateOnly(2026, 1, 5)));
        db.SaveChanges();

        Assert.Equal(ReceiptNumberRules.Format(2026, 2), ReceiptNumbers.Peek(db, new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void BackfillMissing_GivesOlderPaymentsTheirNumbers_OldestFirst()
    {
        using var test = new TestDb();
        using var db = test.Create();
        var (tenant, _) = Seed.TenantWithStay(db);
        var first = PaymentOn(tenant.Id, new DateOnly(2026, 1, 5));
        var second = PaymentOn(tenant.Id, new DateOnly(2026, 2, 5));
        db.Payments.AddRange(first, second);
        db.SaveChanges();

        // simulate payments saved before receipt numbers existed
        db.Database.ExecuteSqlRaw("UPDATE Payments SET ReceiptNo = NULL");
        db.ChangeTracker.Clear();

        ReceiptNumbers.BackfillMissing(db);

        var numbers = db.Payments.OrderBy(p => p.PaymentDate).Select(p => p.ReceiptNo).ToList();
        Assert.Equal(new[] { ReceiptNumberRules.Format(2026, 1), ReceiptNumberRules.Format(2026, 2) }, numbers);
    }
}
