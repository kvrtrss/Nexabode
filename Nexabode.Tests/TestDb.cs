using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexabode.Data;
using Nexabode.Models;
using Nexabode.Services;

// A throwaway database that lives in memory for one test. The app's own code is pointed at it
// (so AuthService, AccountService and the others use it without knowing), and the clock is frozen,
// so a test can check what the app really does on a fixed day.
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDb(DateTime? now = null)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

        using (var db = Create()) db.Database.EnsureCreated();

        AppDb.Factory = Create;
        Clock.Freeze(now ?? new DateTime(2026, 11, 13, 10, 0, 0));
    }

    public AppDbContext Create() => new(_options);

    public void Dispose()
    {
        AppDb.UseDefault();
        Clock.Reset();
        _connection.Dispose();
    }
}

// Small builders for the records the tests need.
public static class Seed
{
    private static int _rooms;

    public static (Tenant Tenant, Occupancy Stay) TenantWithStay(AppDbContext db, string name = "Maria",
        decimal deposit = 0, decimal rent = 1000, DateOnly? moveIn = null)
    {
        var room = new Room { RoomName = $"Room {++_rooms}" };
        var bed = new Bed { Room = room, BedLabel = "Bed 1", MonthlyRate = rent };
        var tenant = new Tenant { FullName = name };
        var stay = new Occupancy
        {
            Tenant = tenant,
            Bed = bed,
            MoveInDate = moveIn ?? new DateOnly(2026, 1, 1),
            AgreedRent = rent,
            Deposit = deposit
        };
        db.Occupancies.Add(stay);
        db.SaveChanges();
        return (tenant, stay);
    }

    public static Charge AddCharge(AppDbContext db, Occupancy stay, decimal amount, DateOnly due, string month)
    {
        var charge = new Charge
        {
            OccupancyId = stay.Id,
            BillingMonth = month,
            ChargeType = ChargeType.Rent,
            Description = "Monthly rent",
            Amount = amount,
            DueDate = due
        };
        db.Charges.Add(charge);
        db.SaveChanges();
        return charge;
    }
}
