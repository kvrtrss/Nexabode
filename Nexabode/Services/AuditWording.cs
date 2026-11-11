using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexabode.Services;

// One recorded change, as stored.
public sealed record AuditItem(int Id, DateTime When, string Administrator, string EntityType, string Field,
                               string? OldValue, string? NewValue, string Reason, bool IsException, int? TenantId);

// What the person reads: one action, in words. One action can have saved several records (editing a stay also
// corrects rent), so the records of one action are shown together.
public sealed class HistoryEvent
{
    public DateTime When { get; init; }
    public string Administrator { get; init; } = "";
    public int? TenantId { get; init; }
    public string Category { get; init; } = "";          // Stay, Billing, Payment, Account or Security
    public string Title { get; init; } = "";             // the main sentence
    public List<string> Details { get; init; } = new();  // the other things that changed with it
    public string Reason { get; init; } = "";
    public bool IsException { get; init; }               // saved although it clashed with another stay
}

// Turns the technical records of the change history into plain sentences and groups them by action.
// Nothing here touches the database or the screens, so it can be tested on its own.
public static class AuditWording
{
    public const string AllCategories = "Everything";
    public static readonly string[] Categories = { AllCategories, "Stay", "Billing", "Payment", "Account", "Security" };

    // ---------- Small formatting helpers ----------

    private static string Empty(string? v) => string.IsNullOrWhiteSpace(v) ? "(empty)" : v.Trim();

    // "2026-11-05" becomes "Nov 5, 2026". Anything else is shown as it was recorded.
    public static string FriendlyDate(string? raw)
    {
        if (DateOnly.TryParseExact((raw ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out var d))
            return Fmt.Date(d);
        return Empty(raw);
    }

    // "1100.00" becomes "₱1,100.00". Anything else is shown as it was recorded.
    public static string FriendlyMoney(string? raw)
    {
        if (decimal.TryParse((raw ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var m))
            return Fmt.Peso(m);
        return Empty(raw);
    }

    // A move-out date is a date, or "no date" while the tenant is still staying.
    private static string MoveOut(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text == "" || text.StartsWith("(none", StringComparison.OrdinalIgnoreCase))
            return "none (still staying)";
        return FriendlyDate(text);
    }

    // A stay is stored as one line: "Room 101 / Bed 2, 2026-11-09 to 2026-11-09, rent 1500.00, deposit 0.00".
    // In words: "Room 101 / Bed 2, Nov 9, 2026 to Nov 9, 2026, rent ₱1,500.00". A zero deposit is left out.
    private static readonly Regex StayPattern = new(
        @"^(?<bed>.*), (?<in>\d{4}-\d{2}-\d{2}) to (?<out>\d{4}-\d{2}-\d{2}|\(none[^)]*\)), rent (?<rent>-?[\d.]+), deposit (?<dep>-?[\d.]+)$",
        RegexOptions.Compiled);

    public static string FriendlyStay(string? raw)
    {
        var match = StayPattern.Match((raw ?? "").Trim());
        if (!match.Success) return Empty(raw);

        var moveIn = FriendlyDate(match.Groups["in"].Value);
        var span = match.Groups["out"].Value.StartsWith("(none")
            ? $"from {moveIn} (still staying)"
            : $"{moveIn} to {FriendlyDate(match.Groups["out"].Value)}";

        var text = $"{match.Groups["bed"].Value}, {span}, rent {FriendlyMoney(match.Groups["rent"].Value)}";
        if (decimal.TryParse(match.Groups["dep"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var dep) && dep != 0)
            text += $", deposit {Fmt.Peso(dep)}";
        return text;
    }

    // "2026-11" becomes "November 2026".
    public static string FriendlyMonth(string? key) =>
        DateTime.TryParseExact((key ?? "").Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out var d)
            ? d.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : Empty(key);

    // "Rent 2026-11" becomes "November 2026 rent". Other names are kept as they are.
    private static string WhatIs(string what)
    {
        var parts = what.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0].Equals("Rent", StringComparison.OrdinalIgnoreCase) &&
            DateTime.TryParseExact(parts[1] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.None, out _))
            return FriendlyMonth(parts[1]) + " rent";
        return what.Trim();
    }

    // A reason of "." or "j" says nothing. Say so, instead of showing the junk.
    public static string ReasonText(string? reason) =>
        ReasonRules.IsReal(reason) ? reason!.Trim() : "No reason given";

    // "Today", "Yesterday", or the date.
    public static string DayLabel(DateTime when, DateTime today)
    {
        var day = DateOnly.FromDateTime(when);
        var now = DateOnly.FromDateTime(today);
        return day == now ? "Today" : day == now.AddDays(-1) ? "Yesterday" : Fmt.Date(day);
    }

    public static string TimeText(DateTime when) =>
        when.ToString("h:mm tt", CultureInfo.InvariantCulture);

    // ---------- One record, in words ----------

    public static string CategoryOf(string entityType) => entityType switch
    {
        "Stay" => "Stay",
        "Charge" or "UtilityBill" => "Billing",
        "Payment" => "Payment",
        "Account" => "Account",
        "Security" => "Security",
        _ => "Other"
    };

    private static readonly Regex LabelPattern = new(@"^(?<what>.+?): (?<kind>[^:]+)$", RegexOptions.Compiled);

    public static string Describe(AuditItem a)
    {
        var field = a.Field.Trim();

        switch (a.EntityType)
        {
            case "Stay":
                return field switch
                {
                    "Created" => $"Stay recorded: {FriendlyStay(a.NewValue)}",
                    "Removed" => $"Stay deleted (it was never billed): {FriendlyStay(a.OldValue)}",
                    "Cancelled" => $"Stay removed after billing, kept as cancelled: {FriendlyStay(a.OldValue)}",
                    "MoveInDate" => $"Move-in date changed from {FriendlyDate(a.OldValue)} to {FriendlyDate(a.NewValue)}",
                    "MoveOutDate" => $"Move-out date changed from {MoveOut(a.OldValue)} to {MoveOut(a.NewValue)}",
                    "MovedOut" => $"Move-out confirmed for {FriendlyDate(a.NewValue)}",
                    "Bed" => $"Bed changed from {Empty(a.OldValue)} to {Empty(a.NewValue)}",
                    "Deposit" => $"Deposit changed from {FriendlyMoney(a.OldValue)} to {FriendlyMoney(a.NewValue)}",
                    "DepositRefunded" => $"Deposit refund changed from {FriendlyMoney(a.OldValue)} to {FriendlyMoney(a.NewValue)}",
                    _ => $"{field}: {Empty(a.OldValue)} to {Empty(a.NewValue)}"
                };

            case "Payment":
                return field == "Receipt printed"
                    ? $"Receipt {Empty(a.NewValue)} printed" +
                      (a.Reason.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ? " (duplicate copy)" : "")
                    : $"{field}: {Empty(a.OldValue)} to {Empty(a.NewValue)}";

            case "Account":
                return field switch
                {
                    "Created" => $"Account created: {Empty(a.NewValue)}",
                    "Password" => "Password changed",
                    "Recovery code" => "Recovery code renewed",
                    "Removed" => $"Account removed: {Empty(a.OldValue)}",
                    "Role" => $"Role changed from {Empty(a.OldValue)} to {Empty(a.NewValue)}",
                    "Username" => $"Username changed from {Empty(a.OldValue)} to {Empty(a.NewValue)}",
                    _ => $"{field}: {Empty(a.OldValue)} to {Empty(a.NewValue)}"
                };

            case "Security":
                return field;

            case "UtilityBill":
                if (field == "Split") return "Utility bill split between tenants";
                if (field == "Split undone") return "Utility bill split undone";
                return Labelled(a, field);

            default: // Charge
                return Labelled(a, field);
        }
    }

    // Charge fields look like "Rent 2026-11: amount" or "Electricity share: voided".
    private static string Labelled(AuditItem a, string field)
    {
        var match = LabelPattern.Match(field);
        if (!match.Success) return $"{field}: {Empty(a.OldValue)} to {Empty(a.NewValue)}";

        var what = WhatIs(match.Groups["what"].Value);
        var kind = match.Groups["kind"].Value.Trim().ToLowerInvariant();

        return kind switch
        {
            "amount" => $"{Cap(what)} amount changed from {FriendlyMoney(a.OldValue)} to {FriendlyMoney(a.NewValue)}",
            "share amount" => $"{Cap(what)} share changed from {FriendlyMoney(a.OldValue)} to {FriendlyMoney(a.NewValue)}",
            "description" => $"{Cap(what)} details changed from {Empty(a.OldValue)} to {Empty(a.NewValue)}",
            "due date" => $"{Cap(what)} due date changed from {FriendlyDate(a.OldValue)} to {FriendlyDate(a.NewValue)}",
            "restored" => $"{Cap(what)} was restored (it had been voided)",
            "voided" or "share voided" => $"{Cap(what)} was voided" +
                (decimal.TryParse((a.OldValue ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                    ? $" ({FriendlyMoney(a.OldValue)})" : ""),
            "adjustment added" => $"{Cap(what)}: an adjustment charge was added" +
                (string.IsNullOrWhiteSpace(a.NewValue) ? "" : $" ({FriendlyMoney(a.NewValue)})"),
            "adjustment needed" => $"{Cap(what)}: an adjustment is needed",
            "share added" => $"{Cap(what)}: a share was added" +
                (string.IsNullOrWhiteSpace(a.NewValue) ? "" : $" ({FriendlyMoney(a.NewValue)})"),
            _ => $"{Cap(what)}: {kind} ({Empty(a.OldValue)} to {Empty(a.NewValue)})"
        };
    }

    private static string Cap(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // ---------- Grouping: several records, one action ----------

    // Which record best names the action. The lower the number, the more it deserves to be the headline.
    private static int Priority(AuditItem a)
    {
        var field = a.Field.ToLowerInvariant();
        if (field.EndsWith(": restored")) return 0;
        if (a.EntityType == "Stay") return 1;
        if (a.EntityType is "Account" or "Security") return 2;
        if (field.EndsWith(": voided")) return 3;
        if (a.EntityType == "Payment") return 4;
        return 5;
    }

    // Records made together (same person, same tenant, same reason, within a few seconds) are one action.
    private static bool SameAction(AuditItem first, AuditItem next) =>
        first.Administrator == next.Administrator &&
        first.TenantId == next.TenantId &&
        first.Reason == next.Reason &&
        Math.Abs((first.When - next.When).TotalSeconds) <= 3;

    // 'newestFirst' is the stored order. The result is newest first too.
    public static List<HistoryEvent> Group(IEnumerable<AuditItem> newestFirst)
    {
        var events = new List<HistoryEvent>();
        var current = new List<AuditItem>();

        void Close()
        {
            if (current.Count == 0) return;

            var oldestFirst = current.OrderBy(a => a.Id).ToList();
            var main = oldestFirst.OrderBy(Priority).ThenBy(a => a.Id).First();
            var details = oldestFirst.Where(a => a != main).Select(Describe).Distinct().ToList();

            events.Add(new HistoryEvent
            {
                When = current[0].When,
                Administrator = current[0].Administrator,
                TenantId = current[0].TenantId,
                Category = CategoryOf(main.EntityType),
                Title = Describe(main),
                Details = details,
                Reason = ReasonText(main.Reason),
                IsException = current.Any(a => a.IsException)
            });
            current = new List<AuditItem>();
        }

        foreach (var item in newestFirst)
        {
            if (current.Count > 0 && !SameAction(current[0], item)) Close();
            current.Add(item);
        }
        Close();
        return events;
    }

    // Does the event match what the person typed in the search box? Looks at everything the card shows.
    public static bool Matches(HistoryEvent e, string tenantName, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var haystack = string.Join(" ", e.Title, string.Join(" ", e.Details), e.Reason, e.Administrator,
                                   tenantName, e.Category);
        return haystack.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
