using System.IO;
using Microsoft.EntityFrameworkCore;
using Nexabode.Models;

namespace Nexabode.Data;

public static class DbInitializer
{
    private const string OneRentRule = "UX_Charges_OneRentPerStayMonth";
    private const string OneShareRule = "UX_Charges_OneShareForEachBillAndStay";
    private const string OneBillRule = "UX_UtilityBills_OnePerMonthUtilityAndScope";

    private static readonly string[] SafetyRuleNames = { OneRentRule, OneShareRule, OneBillRule };

    public static int SafetyRuleCount => SafetyRuleNames.Length;

    // Returns warnings about safety rules that could not be turned on (normally an empty list).
    public static List<string> Initialize()
    {
        using var db = new AppDbContext();

        // Only create the database on a first run. An existing database is opened as it is.
        var path = DatabaseKeyProvider.DbPath;
        var hasData = File.Exists(path) && new FileInfo(path).Length > 0;
        if (!hasData)
            db.Database.EnsureCreated();   // creates the encrypted DB and all tables

        UpgradeSchema(db);
        var warnings = AddSafetyRules(db);

        if (!db.AdminUsers.Any())
        {
            db.AdminUsers.Add(new AdminUser
            {
                // temporary default login, to be changed on first use
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
            });
            db.SaveChanges();
        }

        return warnings;
    }

    // Adds columns introduced after the database was first created (existing data is kept).
    private static void UpgradeSchema(AppDbContext db)
    {
        AddColumnIfMissing(db, "Rooms", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(db, "Beds", "IsMaintenance", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Payments", "IsVoided", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Payments", "VoidReason", "TEXT NULL");
        AddColumnIfMissing(db, "Payments", "VoidedOn", "TEXT NULL");
        AddColumnIfMissing(db, "Charges", "IsVoided", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Charges", "VoidReason", "TEXT NULL");
        AddColumnIfMissing(db, "Charges", "VoidedOn", "TEXT NULL");
    }

    // Rules the database itself enforces, so even two writers at once can't create duplicates.
    private static List<string> AddSafetyRules(AppDbContext db)
    {
        var warnings = new List<string>();

        void Add(string? warning)
        {
            if (warning != null) warnings.Add(warning);
        }

        // One rent charge per stay and month. Voided rent charges still count, so a voided one is never re-created.
        Add(CreateIndexIfSafe(db, OneRentRule,
            "CREATE UNIQUE INDEX " + OneRentRule + " ON Charges (OccupancyId, BillingMonth) WHERE ChargeType = 'Rent'",
            "SELECT COUNT(*) AS Value FROM (SELECT 1 FROM Charges WHERE ChargeType = 'Rent' " +
            "GROUP BY OccupancyId, BillingMonth HAVING COUNT(*) > 1)",
            "Some stays have more than one rent charge for the same month (rule: one rent charge per stay and month)."));

        // One utility share per bill and stay. Voided shares are excluded, so undoing a split and splitting again still works.
        Add(CreateIndexIfSafe(db, OneShareRule,
            "CREATE UNIQUE INDEX " + OneShareRule + " ON Charges (UtilityBillId, OccupancyId) " +
            "WHERE UtilityBillId IS NOT NULL AND IsVoided = 0",
            "SELECT COUNT(*) AS Value FROM (SELECT 1 FROM Charges WHERE UtilityBillId IS NOT NULL AND IsVoided = 0 " +
            "GROUP BY UtilityBillId, OccupancyId HAVING COUNT(*) > 1)",
            "Some utility bills were split more than once for the same tenant (rule: one share per bill and stay)."));

        // One utility bill per month, utility type, and room or whole house.
        // The expression keeps "no room" (whole house) from counting as different each time.
        Add(CreateIndexIfSafe(db, OneBillRule,
            "CREATE UNIQUE INDEX " + OneBillRule +
            " ON UtilityBills (BillingMonth, lower(UtilityType), ifnull(RoomId, 0))",
            "SELECT COUNT(*) AS Value FROM (SELECT 1 FROM UtilityBills " +
            "GROUP BY BillingMonth, lower(UtilityType), ifnull(RoomId, 0) HAVING COUNT(*) > 1)",
            "Some months have the same utility bill entered twice (rule: one bill per month, utility, and room)."));

        return warnings;
    }

    public static int CountActiveSafetyRules(AppDbContext db) => SafetyRuleNames.Count(name => IndexExists(db, name));

#pragma warning disable EF1002   // the table, column, and index names below are fixed constants, never user input
    private static bool IndexExists(AppDbContext db, string name) =>
        db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = '{name}'")
            .AsEnumerable()
            .First() > 0;

    // Creates the index unless it exists, or unless the data already breaks the rule (then it returns a warning).
    private static string? CreateIndexIfSafe(AppDbContext db, string indexName, string createSql,
                                             string duplicateCountSql, string warning)
    {
        if (IndexExists(db, indexName)) return null;

        var duplicates = db.Database.SqlQueryRaw<int>(duplicateCountSql).AsEnumerable().First();
        if (duplicates > 0) return warning;

        db.Database.ExecuteSqlRaw(createSql);
        return null;
    }

    private static void AddColumnIfMissing(AppDbContext db, string table, string column, string definition)
    {
        var exists = db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = '{column}'")
            .AsEnumerable()
            .First();

        if (exists == 0)
            db.Database.ExecuteSqlRaw($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
    }
#pragma warning restore EF1002
}