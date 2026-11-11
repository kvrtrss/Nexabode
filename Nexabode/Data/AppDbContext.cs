using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexabode.Models;
using Nexabode.Services;
using static Nexabode.Models.PaymentAllocation;

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
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<SignInGuard> SignInGuards => Set<SignInGuard>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(DatabaseKeyProvider.BuildConnectionString(
            DatabaseKeyProvider.DbPath, DatabaseKeyProvider.GetOrCreateKey()));
    }

    // Every new payment gets its receipt number here, so it can't be forgotten by any screen that records a payment.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var added = ChangeTracker.Entries<Payment>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();
        if (added.Count > 0) ReceiptNumbers.Assign(this, added);

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>().HasIndex(p => p.ReceiptNo).IsUnique();
        modelBuilder.Entity<AdminUser>().HasIndex(a => a.Username).IsUnique();
        modelBuilder.Entity<Room>().HasIndex(r => r.RoomName).IsUnique();
        modelBuilder.Entity<Bed>().HasIndex(b => new { b.RoomId, b.BedLabel }).IsUnique();
        modelBuilder.Entity<Charge>().Property(c => c.ChargeType).HasConversion<string>();
        // Voided charges disappear from every query unless a page asks to see them.
        modelBuilder.Entity<Charge>().HasQueryFilter(c => !c.IsVoided);
        modelBuilder.Entity<PaymentAllocation>().HasQueryFilter(a => !a.Charge.IsVoided);
        // A cancelled stay disappears from every query (beds, clashes, rent, reports) unless a page asks for it.
        modelBuilder.Entity<Occupancy>().HasQueryFilter(o => !o.IsCancelled);

        // Never auto-delete history: block deleting a record that other records depend on.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}