using Microsoft.EntityFrameworkCore;
using Nexabode.Models;
using System.IO;

namespace Nexabode.Data;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();

        // Only create the database on a first run. An existing database is opened as it is.
        var path = DatabaseKeyProvider.DbPath;
        var hasData = File.Exists(path) && new FileInfo(path).Length > 0;
        if (!hasData)
            db.Database.EnsureCreated();   // creates the encrypted DB and all tables

        UpgradeSchema(db);

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

#pragma warning disable EF1002   // table and column names below are fixed constants, never user input
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