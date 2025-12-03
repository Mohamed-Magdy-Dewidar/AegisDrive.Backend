using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AegisDrive.API.DataBase.Configurations;


public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");


        builder.HasKey(v => v.Id);
        builder.Property(v => v.PlateNumber).IsRequired().HasMaxLength(20);
        builder.Property(v => v.Model).HasMaxLength(50);
       

        builder.HasIndex(v => v.PlateNumber).IsUnique();

        builder.Property(v => v.Status)
                   .HasConversion(
                       convertToProviderExpression: (VehicleStatus) => VehicleStatus.ToString(),
                       convertFromProviderExpression: (_vehicleStatus) => (VehicleStatus)Enum.Parse(typeof(VehicleStatus), _vehicleStatus )
                   )          
                   .HasMaxLength(20)
                   .HasDefaultValue(VehicleStatus.Active);

  

        // Relationship for Fast Lookup: Vehicle -> CurrentDriver
        builder.HasOne(v => v.CurrentDriver)
               .WithMany() // Driver doesn't need a collection of "CurrentVehicles"
               .HasForeignKey(v => v.CurrentDriverId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}