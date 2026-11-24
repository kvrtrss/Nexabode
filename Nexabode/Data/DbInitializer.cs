using System.IO;
using Microsoft.EntityFrameworkCore;
using Nexabode.Models;
using Nexabode.Services;

namespace Nexabode.Data;

public static class DbInitializer
{
    private const string OneRentRule = "UX_Charges_OneRentPerStayMonth";
    private const string OneShareRule = "UX_Charges_OneShareForEachBillAndStay";
    private const string OneBillRule = "UX_UtilityBills_OnePerMonthUtilityAndScope";

    private static readonly string[] SafetyRuleNames = { OneRentRule, OneShareRule, OneBillRule };

    public static int SafetyRuleCount => SafetyRuleNames.Length;

    // The version of the database layout this build expects, stored inside the database (PRAGMA user_version).
    // When a new column or table is added to UpgradeSchema, raise this number by one.
    // Version 1: first versioned layout. Version 2: payments made from a deposit have the method "Deposit" (they used to say "Credit").
    public const int SchemaVersion = 2;

    // Returns warnings about safety rules that could not be turned on (normally an empty list).
    public static List<string> Initialize()
    {
        using var db = AppDb.Create();

        // Only create the database on a first run. An existing database is opened as it is.
        var path = DatabaseKeyProvider.DbPath;
        var hasData = File.Exists(path) && new FileInfo(path).Length > 0;
        if (!hasData)
            db.Database.EnsureCreated();   // creates the encrypted DB and all tables

        UpgradeIfNeeded(db, hasData);
        ReceiptNumbers.BackfillMissing(db);   // payments from before receipt numbers get one too

        var warnings = AddSafetyRules(db);

        // No default account is made: on a first run the first Owner account is created on the setup screen.

        return warnings;
    }

    // Upgrades the database only when its stored version is older than this build expects.
    // The safety copy is made once per upgrade (not on every start), and the upgrade runs in one
    // transaction, so a failure part-way leaves the database exactly as it was.
    private static void UpgradeIfNeeded(AppDbContext db, bool hasData)
    {
        var current = GetSchemaVersion(db);

        if (current > SchemaVersion)
            throw new InvalidOperationException(
                $"This data was made by a newer version of Nexabode (data version {current}; this copy supports up to {SchemaVersion}). " +
                "Install the latest Nexabode, or restore an older backup.");

        if (current == SchemaVersion) return;

        if (hasData) BackupBeforeUpgrade(db);

        using var transaction = db.Database.BeginTransaction();
        UpgradeSchema(db);
#pragma warning disable EF1003   // SchemaVersion is a fixed number in this file, never user input
        db.Database.ExecuteSqlRaw("PRAGMA user_version = " + SchemaVersion);
#pragma warning restore EF1003
        transaction.Commit();
    }

    private static int GetSchemaVersion(AppDbContext db) =>
        db.Database
            .SqlQueryRaw<int>("SELECT user_version AS Value FROM pragma_user_version")
            .AsEnumerable()
            .First();

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
        AddColumnIfMissing(db, "Occupancies", "DepositRefunded", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Occupancies", "DepositRefundedOn", "TEXT NULL");
        AddColumnIfMissing(db, "Occupancies", "DepositRefundNote", "TEXT NULL");
        AddColumnIfMissing(db, "Occupancies", "MovedOutOn", "TEXT NULL");
        AddColumnIfMissing(db, "Payments", "Source", "TEXT NULL");
        AddColumnIfMissing(db, "Payments", "AmountReceived", "TEXT NULL");
        AddColumnIfMissing(db, "Charges", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Charges", "AdjustsChargeId", "INTEGER NULL");
        AddColumnIfMissing(db, "Charges", "RefundedAmount", "TEXT NOT NULL DEFAULT '0'");
        AddColumnIfMissing(db, "Charges", "RefundedOn", "TEXT NULL");
        AddColumnIfMissing(db, "Payments", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "UtilityBills", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "UtilityBills", "PeriodStart", "TEXT NULL");
        AddColumnIfMissing(db, "UtilityBills", "PeriodEnd", "TEXT NULL");
        AddColumnIfMissing(db, "AdminUsers", "Role", "TEXT NOT NULL DEFAULT 'Owner'");
        AddColumnIfMissing(db, "AdminUsers", "MustChangePassword", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Payments", "ReceiptNo", "TEXT NULL");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Payments_ReceiptNo ON Payments (ReceiptNo)");
        AddColumnIfMissing(db, "AdminUsers", "RecoveryCodeHash", "TEXT NULL");
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS SignInGuards (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "FailedAttempts INTEGER NOT NULL DEFAULT 0, " +
            "LockLevel INTEGER NOT NULL DEFAULT 0, " +
            "LockedUntilUtc TEXT NULL)");
        AddColumnIfMissing(db, "SignInGuards", "EmergencyKeyHash", "TEXT NULL");
        AddColumnIfMissing(db, "SignInGuards", "ResetPhraseHash", "TEXT NULL");
        AddColumnIfMissing(db, "Tenants", "Gender", "TEXT NULL");
        AddColumnIfMissing(db, "Occupancies", "IsCancelled", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(db, "Occupancies", "CancelReason", "TEXT NULL");
        AddColumnIfMissing(db, "Occupancies", "CancelledOn", "TEXT NULL");
        db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS AuditLog (" +
        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
        "ChangedAt TEXT NOT NULL, " +
        "Administrator TEXT NOT NULL, " +
        "EntityType TEXT NOT NULL, " +
        "EntityId INTEGER NOT NULL, " +
        "TenantId INTEGER NULL, " +
        "Field TEXT NOT NULL, " +
        "OldValue TEXT NULL, " +
        "NewValue TEXT NULL, " +
        "Reason TEXT NOT NULL, " +
        "IsConflictException INTEGER NOT NULL DEFAULT 0)");
            db.Database.ExecuteSqlRaw(
                "CREATE INDEX IF NOT EXISTS IX_AuditLog_Entity ON AuditLog (EntityType, EntityId)");
        db.Database.ExecuteSqlRaw(
        "CREATE TABLE IF NOT EXISTS Reminders (" +
        "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
        "Title TEXT NOT NULL, " +
        "Note TEXT NULL, " +
        "DueDate TEXT NOT NULL, " +
        "IsDone INTEGER NOT NULL DEFAULT 0)");

        // Version 2: money paid out of a tenant's deposit is called "Deposit" everywhere. Older payments said "Credit".
        db.Database.ExecuteSqlRaw("UPDATE Payments SET Method = 'Deposit' WHERE Method = 'Credit'");
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
            "Some stays have more than one rent charge for the same month. Each stay should have only one rent charge per month."));

        // One utility share per bill and stay. Voided shares are excluded, so undoing a split and splitting again still works.
        Add(CreateIndexIfSafe(db, OneShareRule,
            "CREATE UNIQUE INDEX " + OneShareRule + " ON Charges (UtilityBillId, OccupancyId) " +
            "WHERE UtilityBillId IS NOT NULL AND IsVoided = 0",
            "SELECT COUNT(*) AS Value FROM (SELECT 1 FROM Charges WHERE UtilityBillId IS NOT NULL AND IsVoided = 0 " +
            "GROUP BY UtilityBillId, OccupancyId HAVING COUNT(*) > 1)",
            "Some utility bills have been split more than once for the same stay. Each stay should have only one share of each bill."));

        // One utility bill per month, utility type, and room or whole house.
        // The expression keeps "no room" (whole house) from counting as different each time.
        Add(CreateIndexIfSafe(db, OneBillRule,
            "CREATE UNIQUE INDEX " + OneBillRule +
            " ON UtilityBills (BillingMonth, lower(UtilityType), ifnull(RoomId, 0))",
            "SELECT COUNT(*) AS Value FROM (SELECT 1 FROM UtilityBills " +
            "GROUP BY BillingMonth, lower(UtilityType), ifnull(RoomId, 0) HAVING COUNT(*) > 1)",
            "Some months have the same utility bill entered more than once. Only one bill is allowed for each month, utility type and room."));

        return warnings;
    }

    public static int CountActiveSafetyRules(AppDbContext db) => SafetyRuleNames.Count(name => IndexExists(db, name));

    // Copies the encrypted database before an upgrade. If the copy fails, the upgrade is stopped.
    // Only the newest few copies are kept, so the folder can't grow without limit.
    private static void BackupBeforeUpgrade(AppDbContext db)
    {
        var folder = Path.Combine(DatabaseKeyProvider.AppFolder, "SafetyCopies");

        try
        {
            db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");

            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, $"before-upgrade-{DateTime.Now:yyyyMMdd-HHmmss}.db");

            using var source = new FileStream(DatabaseKeyProvider.DbPath, FileMode.Open,
                                              FileAccess.Read, FileShare.ReadWrite);
            using var copy = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
            source.CopyTo(copy);
        }
        catch (Exception ex)
        {
            throw new IOException("A safety copy of your data could not be saved, so the upgrade was stopped. Details: " +
                                  ex.Message, ex);
        }

        PruneSafetyCopies(folder, keep: 5);
    }

    // Deletes the oldest automatic copies. Not critical, so a failure here never stops the app.
    private static void PruneSafetyCopies(string folder, int keep)
    {
        try
        {
            var old = new DirectoryInfo(folder).GetFiles("before-upgrade-*.db")
                .OrderByDescending(f => f.Name)   // the name holds the date and time, so this is newest first
                .Skip(keep);

            foreach (var file in old) file.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

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