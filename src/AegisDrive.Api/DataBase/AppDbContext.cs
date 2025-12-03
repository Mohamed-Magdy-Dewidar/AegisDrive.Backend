using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AegisDrive.Api.DataBase;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {}

    public DbSet<Company> Companies { get; set; }
    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Device> Devices { get; set; }

    //  Assignments (Junction Table)
    public DbSet<VehicleAssignment> VehicleAssignments { get; set; }

    public DbSet<SafetyEvent> SafetyEvents { get; set; }
    public DbSet<TelemetryEvent> TelemetryEvents { get; set; }

    public DbSet<FamilyMember> FamilyMembers { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssemblyRefrence).Assembly);
    }

}