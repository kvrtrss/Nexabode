using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;

namespace Nexabode.Services;

// Where "Open" takes the administrator for a notification.
public enum NoticeTarget { None, Tenants, RentBilling, UtilityBills, BillingCharges, Payments, Settings }

public class Notice
{
    // Identifies the situation. If it changes (more is owed, a new day is reached) the key changes,
    // so a notification the administrator already read comes back as new.
    public string Key { get; init; } = "";
    public string Tone { get; init; } = "Info";   // Danger, Warning or Info (the app's own colour names)
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public DateOnly? Date { get; init; }
    public NoticeTarget Target { get; init; }
    public int? ReminderId { get; init; }         // set for the administrator's own reminders

    public int Rank => Tone switch { "Danger" => 0, "Warning" => 1, _ => 2 };
}

// Works out what needs the administrator's attention. Nothing is stored: a notification appears while its
// cause exists (an unpaid charge, a stay ending today, a missing backup) and goes away once it is fixed.
// Only the administrator's own reminders are saved.
public static class NotificationService
{
    public const int DefaultLeadDays = 3;

    public static int ClampLeadDays(int days) => Math.Clamp(days, 1, 30);

    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    public static List<Notice> Build(AppDbContext db, DateOnly today, int leadDays)
    {
        var soon = today.AddDays(ClampLeadDays(leadDays));
        var list = new List<Notice>();

        AddChargeNotices(db, today, soon, list);
        AddStayNotices(db, today, soon, list);
        AddRentNotice(db, today, list);
        AddUtilityNotices(db, list);
        AddCreditNotices(db, today, list);
        AddReminderNotices(db, today, soon, list);
        AddBackupNotice(list);

        return list
            .OrderBy(n => n.Rank)
            .ThenBy(n => n.Date ?? DateOnly.MaxValue)
            .ThenBy(n => n.Title)
            .ToList();
    }

    // ---------- Read / unread ----------

    public static HashSet<string> ReadKeys() => (AppSettings.Load().ReadNoticeKeys ?? new()).ToHashSet();

    public static int UnreadCount(IEnumerable<Notice> notices)
    {
        var read = ReadKeys();
        return notices.Count(n => !read.Contains(n.Key));
    }

    public static void MarkRead(IEnumerable<string> keys) =>
        AppSettings.Update(s =>
        {
            s.ReadNoticeKeys ??= new();
            foreach (var key in keys)
                if (!s.ReadNoticeKeys.Contains(key)) s.ReadNoticeKeys.Add(key);

            // Old keys of problems that were fixed long ago are dropped so the list stays small.
            if (s.ReadNoticeKeys.Count > 500)
                s.ReadNoticeKeys.RemoveRange(0, s.ReadNoticeKeys.Count - 500);
        });

    // Marks everything shown right now as read, and forgets keys that no longer apply.
    public static void MarkAllRead(IEnumerable<Notice> current) =>
        AppSettings.Update(s => s.ReadNoticeKeys = current.Select(n => n.Key).Distinct().ToList());

    // Every reminder that is not done yet, soonest first (the page lists them all, even far-off ones).
    public static List<Reminder> OpenReminders(AppDbContext db) =>
        db.Reminders.Where(r => !r.IsDone).ToList().OrderBy(r => r.DueDate).ThenBy(r => r.Id).ToList();

    // ---------- Payments ----------

    private static string ItemText(IEnumerable<Charge> charges)
    {
        var names = charges
            .Select(c => string.IsNullOrWhiteSpace(c.Description) ? c.ChargeType.ToString() : c.Description!)
            .Distinct()
            .ToList();
        return names.Count <= 3
            ? string.Join(", ", names)
            : string.Join(", ", names.Take(3)) + $" and {names.Count - 3} more";
    }

    private static void AddChargeNotices(AppDbContext db, DateOnly today, DateOnly soon, List<Notice> list)
    {
        // Money is added up in C# because SQLite can't add up decimal columns.
        var open = db.Charges
            .Include(c => c.Allocations)
            .Include(c => c.Occupancy).ThenInclude(o => o.Tenant)
            .ToList()
            .Select(c => new
            {
                Charge = c,
                TenantId = c.Occupancy.TenantId,
                Tenant = c.Occupancy.Tenant.FullName,
                Balance = c.Amount - c.Allocations.Sum(a => a.AmountApplied)
            })
            .Where(x => x.Balance > 0)
            .ToList();

        foreach (var tenant in open.GroupBy(x => x.TenantId))
        {
            var name = tenant.First().Tenant;

            var overdue = tenant.Where(x => x.Charge.DueDate < today).ToList();
            if (overdue.Count > 0)
            {
                var oldest = overdue.Min(x => x.Charge.DueDate);
                var late = today.DayNumber - oldest.DayNumber;
                list.Add(new Notice
                {
                    Key = $"overdue:{tenant.Key}:{overdue.Sum(x => x.Balance):0.00}:{oldest:yyyyMMdd}",
                    Tone = "Danger",
                    Category = "OVERDUE",
                    Title = $"{name} owes {Fmt.Peso(overdue.Sum(x => x.Balance))} past the due date",
                    Detail = $"{ItemText(overdue.Select(x => x.Charge))}. The oldest was due {Fmt.Date(oldest)} ({Days(late)} late).",
                    Date = oldest,
                    Target = NoticeTarget.BillingCharges
                });
            }

            var dueToday = tenant.Where(x => x.Charge.DueDate == today).ToList();
            if (dueToday.Count > 0)
            {
                list.Add(new Notice
                {
                    Key = $"today:{tenant.Key}:{dueToday.Sum(x => x.Balance):0.00}",
                    Tone = "Warning",
                    Category = "DUE TODAY",
                    Title = $"{name} has {Fmt.Peso(dueToday.Sum(x => x.Balance))} due today",
                    Detail = ItemText(dueToday.Select(x => x.Charge)) + ".",
                    Date = today,
                    Target = NoticeTarget.BillingCharges
                });
            }

            var upcoming = tenant.Where(x => x.Charge.DueDate > today && x.Charge.DueDate <= soon).ToList();
            if (upcoming.Count > 0)
            {
                var next = upcoming.Min(x => x.Charge.DueDate);
                list.Add(new Notice
                {
                    Key = $"soon:{tenant.Key}:{upcoming.Sum(x => x.Balance):0.00}:{next:yyyyMMdd}",
                    Tone = "Info",
                    Category = "DUE SOON",
                    Title = $"{name} has {Fmt.Peso(upcoming.Sum(x => x.Balance))} coming due",
                    Detail = $"{ItemText(upcoming.Select(x => x.Charge))}. Next due {Fmt.Date(next)}, in {Days(next.DayNumber - today.DayNumber)}.",
                    Date = next,
                    Target = NoticeTarget.BillingCharges
                });
            }
        }
    }

    // ---------- Move-ins and move-outs ----------

    private static void AddStayNotices(AppDbContext db, DateOnly today, DateOnly soon, List<Notice> list)
    {
        var stays = db.Occupancies
            .Include(o => o.Tenant)
            .Include(o => o.Bed).ThenInclude(b => b.Room)
            .ToList();

        foreach (var s in stays)
        {
            var name = s.Tenant.FullName;
            var bed = $"{s.Bed.Room.RoomName} / {s.Bed.BedLabel}";

            if (s.MoveInDate == today)
            {
                list.Add(new Notice
                {
                    Key = $"movein:{s.Id}:{s.MoveInDate:yyyyMMdd}",
                    Tone = "Info",
                    Category = "MOVE-IN",
                    Title = $"{name} moves in today",
                    Detail = bed + ".",
                    Date = today,
                    Target = NoticeTarget.Tenants
                });
            }
            else if (s.MoveInDate > today && s.MoveInDate <= soon)
            {
                list.Add(new Notice
                {
                    Key = $"movein:{s.Id}:{s.MoveInDate:yyyyMMdd}",
                    Tone = "Info",
                    Category = "MOVE-IN",
                    Title = $"{name} moves in on {Fmt.Date(s.MoveInDate)}",
                    Detail = $"{bed}, in {Days(s.MoveInDate.DayNumber - today.DayNumber)}.",
                    Date = s.MoveInDate,
                    Target = NoticeTarget.Tenants
                });
            }

            // A stay already confirmed as moved out needs nothing more.
            if (s.MovedOutOn != null || s.MoveInDate > today || s.MoveOutDate is not DateOnly last) continue;

            if (last == today)
            {
                list.Add(new Notice
                {
                    Key = $"moveout:{s.Id}:{last:yyyyMMdd}",
                    Tone = "Warning",
                    Category = "MOVE-OUT",
                    Title = $"{name}'s last day is today",
                    Detail = $"{bed}. Press Move out on the Tenants page once they have left.",
                    Date = today,
                    Target = NoticeTarget.Tenants
                });
            }
            else if (last > today && last <= soon)
            {
                list.Add(new Notice
                {
                    Key = $"moveout:{s.Id}:{last:yyyyMMdd}",
                    Tone = "Info",
                    Category = "MOVE-OUT",
                    Title = $"{name} moves out on {Fmt.Date(last)}",
                    Detail = $"{bed}, in {Days(last.DayNumber - today.DayNumber)}.",
                    Date = last,
                    Target = NoticeTarget.Tenants
                });
            }
        }
    }

    // ---------- Billing that has not been done yet ----------

    private static void AddRentNotice(AppDbContext db, DateOnly today, List<Notice> list)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var key = BillingHelpers.MonthKey(monthStart);

        // Voided rent charges count as billed, the same rule Rent Billing uses.
        var billed = db.Charges.IgnoreQueryFilters()
            .Where(c => c.ChargeType == ChargeType.Rent && c.BillingMonth == key)
            .Select(c => c.OccupancyId)
            .ToList()
            .ToHashSet();

        var missing = db.Occupancies.Include(o => o.Tenant).ToList()
            .Where(o => !billed.Contains(o.Id)
                        && StayRules.DaysIn(new StayRange(o.Id, o.MoveInDate, o.MoveOutDate), monthStart) > 0)
            .Select(o => o.Tenant.FullName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (missing.Count == 0) return;

        var names = missing.Count <= 4
            ? string.Join(", ", missing)
            : string.Join(", ", missing.Take(4)) + $" and {missing.Count - 4} more";

        list.Add(new Notice
        {
            Key = $"rent:{key}:{missing.Count}",
            Tone = "Warning",
            Category = "RENT",
            Title = $"Rent for {monthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture)} is not generated for {missing.Count} tenant(s)",
            Detail = names + ". Open Rent Billing and press Generate rent.",
            Date = monthStart,
            Target = NoticeTarget.RentBilling
        });
    }

    private static void AddUtilityNotices(AppDbContext db, List<Notice> list)
    {
        var bills = db.UtilityBills
            .Include(b => b.Room)
            .Include(b => b.Charges)
            .Where(b => !b.IsArchived)
            .ToList()
            .Where(b => b.Charges.Count == 0);   // no shares yet, so no tenant has been charged

        foreach (var b in bills)
        {
            DateOnly? monthDate = DateOnly.TryParseExact(b.BillingMonth + "-01", "yyyy-MM-dd",
                                      CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed : null;
            var month = monthDate?.ToString("MMMM yyyy", CultureInfo.CurrentCulture) ?? b.BillingMonth;

            list.Add(new Notice
            {
                Key = $"utility:{b.Id}",
                Tone = "Warning",
                Category = "UTILITY",
                Title = $"{b.UtilityType} bill for {month} is not split yet",
                Detail = $"{Fmt.Peso(b.TotalAmount)} for {(b.Room?.RoomName ?? "the whole house")}. " +
                         "Split it on Utility Bills so tenants are charged their share.",
                Date = monthDate,
                Target = NoticeTarget.UtilityBills
            });
        }
    }

    // ---------- Credit left with a tenant who has gone ----------

    private static void AddCreditNotices(AppDbContext db, DateOnly today, List<Notice> list)
    {
        var used = db.Payments
            .Where(p => !p.IsVoided && p.Source == DepositService.PaymentSource)
            .ToList()
            .GroupBy(p => p.TenantId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var names = db.Tenants.ToDictionary(t => t.Id, t => t.FullName);

        foreach (var tenant in db.Occupancies.ToList().GroupBy(o => o.TenantId))
        {
            // Someone still staying can use their credit; only a tenant who has left needs a refund decision.
            var stillHere = tenant.Any(o => o.MovedOutOn == null && (o.MoveOutDate == null || o.MoveOutDate >= today));
            if (stillHere) continue;

            used.TryGetValue(tenant.Key, out var applied);
            var held = new DepositSummary(tenant.Sum(o => o.Deposit), tenant.Sum(o => o.DepositRefunded), applied).Held;
            if (held <= 0 || !names.TryGetValue(tenant.Key, out var name)) continue;

            list.Add(new Notice
            {
                Key = $"credit:{tenant.Key}:{held:0.00}",
                Tone = "Info",
                Category = "CREDIT",
                Title = $"{name} has {Fmt.Peso(held)} credit left",
                Detail = "They have moved out. Refund the credit, or apply it to unpaid charges, on the Tenants page.",
                Target = NoticeTarget.Tenants
            });
        }
    }

    // ---------- The administrator's own reminders ----------

    private static void AddReminderNotices(AppDbContext db, DateOnly today, DateOnly soon, List<Notice> list)
    {
        foreach (var r in OpenReminders(db).Where(r => r.DueDate <= soon))
        {
            var when = r.DueDate < today ? $"Was due {Fmt.Date(r.DueDate)}."
                : r.DueDate == today ? "Due today."
                : $"Due {Fmt.Date(r.DueDate)}, in {Days(r.DueDate.DayNumber - today.DayNumber)}.";

            var state = r.DueDate < today ? "late" : r.DueDate == today ? "today" : "soon";

            list.Add(new Notice
            {
                Key = $"reminder:{r.Id}:{state}",
                Tone = r.DueDate < today ? "Danger" : r.DueDate == today ? "Warning" : "Info",
                Category = "REMINDER",
                Title = r.Title,
                Detail = string.IsNullOrWhiteSpace(r.Note) ? when : $"{when} {r.Note!.Trim()}",
                Date = r.DueDate,
                ReminderId = r.Id
            });
        }
    }

    // ---------- Backup ----------

    private static void AddBackupNotice(List<Notice> list)
    {
        if (!BackupReminder.IsDue) return;

        var days = BackupReminder.DaysSinceBackup();
        var lastBackup = BackupReminder.LastBackupTime()?.ToString("yyyyMMdd") ?? "never";
        list.Add(new Notice
        {
            Key = $"backup:{lastBackup}",
            Tone = "Warning",
            Category = "BACKUP",
            Title = days == null ? "You haven't made a backup yet" : $"Your last backup was {days} day(s) ago",
            Detail = "Make a fresh backup in Settings so your recent records are protected.",
            Target = NoticeTarget.Settings
        });
    }
}