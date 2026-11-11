using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

// Gives payments their receipt numbers. Nobody types a receipt number: it is assigned when the payment is saved.
public static class ReceiptNumbers
{
    // The number the next payment dated in this year will get. Only a preview for the screen: the real number
    // is given when the payment is saved, so nothing here changes the database.
    public static string Peek(AppDbContext db, DateOnly paymentDate)
    {
        var prefix = ReceiptNumberRules.YearPrefix(paymentDate.Year);
        var existing = db.Payments
            .Where(p => p.ReceiptNo != null && p.ReceiptNo.StartsWith(prefix))
            .Select(p => p.ReceiptNo)
            .ToList();
        return ReceiptNumberRules.NextNumber(existing, paymentDate.Year);
    }

    // Numbers the payments that have none, oldest first, continuing from the highest number of each year.
    public static void Assign(AppDbContext db, IEnumerable<Payment> payments)
    {
        var todo = payments.Where(p => string.IsNullOrEmpty(p.ReceiptNo))
                           .OrderBy(p => p.PaymentDate).ThenBy(p => p.Id)
                           .ToList();

        foreach (var year in todo.GroupBy(p => p.PaymentDate.Year))
        {
            var prefix = ReceiptNumberRules.YearPrefix(year.Key);
            var existing = db.Payments
                .Where(p => p.ReceiptNo != null && p.ReceiptNo.StartsWith(prefix))
                .Select(p => p.ReceiptNo)
                .ToList();

            var next = ReceiptNumberRules.NextSequence(existing, year.Key);
            foreach (var payment in year)
                payment.ReceiptNo = ReceiptNumberRules.Format(year.Key, next++);
        }
    }

    // Used once when upgrading: numbers the payments recorded before receipt numbers existed.
    public static void BackfillMissing(AppDbContext db)
    {
        var missing = db.Payments.Where(p => p.ReceiptNo == null).ToList();
        if (missing.Count == 0) return;

        Assign(db, missing);
        db.SaveChanges();
    }
}
