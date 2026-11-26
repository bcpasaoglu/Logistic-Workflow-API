namespace LogisticDesk.Data;
using LogisticDesk.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class LogiDbContext : IdentityDbContext<ApplicationUser>
{
    public LogiDbContext(DbContextOptions<LogiDbContext> options) : base(options)
    {
    }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Shipment> Shipments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Weights and capacities are stored with two decimal places (kg).
        builder.Entity<Shipment>().Property(s => s.Weight).HasPrecision(18, 2);
        builder.Entity<Vehicle>().Property(v => v.MaxCapacity).HasPrecision(18, 2);

        // Tracking numbers are looked up by customers, so keep them unique and indexed.
        builder.Entity<Shipment>().Property(s => s.TrackingNumber).HasMaxLength(32);
        builder.Entity<Shipment>().HasIndex(s => s.TrackingNumber).IsUnique();
    }
}
