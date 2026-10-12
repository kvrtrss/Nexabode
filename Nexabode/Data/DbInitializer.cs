using Nexabode.Models;

namespace Nexabode.Data;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var db = new AppDbContext();
        db.Database.EnsureCreated();   // creates the encrypted DB and all tables if missing

        if (!db.AdminUsers.Any())
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")  // temporary infos, the user will change this once they log in for the first time
            });
            db.SaveChanges();
        }
    }
}