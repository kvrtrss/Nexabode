using Microsoft.EntityFrameworkCore;
using Nexabode.Models;

namespace Nexabode.Data;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();   // creates the encrypted DB and all tables if missing

        UpgradeSchema(db);

        if (!db.AdminUsers.Any())
        {
            db.AdminUsers.Add(new AdminUser
            {
                // temporary default login, to be changed by the user on first use
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
            });
            db.SaveChanges();
        }
    }

    // Adds columns introduced after the database was first created.
    private static void UpgradeSchema(AppDbContext db)
    {
        var hasRoomStatus = db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Rooms') WHERE name = 'IsActive'")
            .AsEnumerable()
            .First();

        if (hasRoomStatus == 0)
            db.Database.ExecuteSqlRaw("ALTER TABLE Rooms ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1");
    }
}