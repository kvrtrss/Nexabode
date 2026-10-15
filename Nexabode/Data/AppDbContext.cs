using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexabode.Models;

namespace Nexabode.Data;

public class AppDbContext : DbContext
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Occupancy> Occupancies => Set<Occupancy>();
    public DbSet<UtilityBill> UtilityBills => Set<UtilityBill>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(DatabaseKeyProvider.BuildConnectionString(
            DatabaseKeyProvider.DbPath, DatabaseKeyProvider.GetOrCreateKey()));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>().HasIndex(a => a.Username).IsUnique();
        modelBuilder.Entity<Room>().HasIndex(r => r.RoomName).IsUnique();
        modelBuilder.Entity<Bed>().HasIndex(b => new { b.RoomId, b.BedLabel }).IsUnique();
        modelBuilder.Entity<Charge>().Property(c => c.ChargeType).HasConversion<string>();
        // Voided charges disappear from every query unless a page asks to see them.
        modelBuilder.Entity<Charge>().HasQueryFilter(c => !c.IsVoided);
        modelBuilder.Entity<PaymentAllocation>().HasQueryFilter(a => !a.Charge.IsVoided);

        // Never auto-delete history: block deleting a record that other records depend on.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}