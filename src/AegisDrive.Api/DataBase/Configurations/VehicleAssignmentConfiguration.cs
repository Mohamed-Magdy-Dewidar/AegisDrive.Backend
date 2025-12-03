using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AegisDrive.API.DataBase.Configurations;

public class VehicleAssignmentConfiguration : IEntityTypeConfiguration<VehicleAssignment>
{
    public void Configure(EntityTypeBuilder<VehicleAssignment> builder)
    {
        builder.ToTable("VehicleAssignments");


        builder.HasKey(va => va.Id);

        // A vehicle can have ONLY ONE active assignment
        builder.HasIndex(va => new { va.VehicleId, va.UnassignedAt })
               .IsUnique()
               .HasFilter("[UnassignedAt] IS NULL");

        // A driver can have ONLY ONE active assignment
        builder.HasIndex(va => new { va.DriverId, va.UnassignedAt })
               .IsUnique()
               .HasFilter("[UnassignedAt] IS NULL");

        builder.HasOne(va => va.Driver)
               .WithMany(d => d.VehicleAssignments)
               .HasForeignKey(va => va.DriverId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(va => va.Vehicle)
               .WithMany(v => v.VehicleAssignments)
               .HasForeignKey(va => va.VehicleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
