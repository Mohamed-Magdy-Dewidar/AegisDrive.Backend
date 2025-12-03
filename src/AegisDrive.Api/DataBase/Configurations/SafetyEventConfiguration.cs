using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using AegisDrive.Api.Entities.Enums.Driver;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AegisDrive.API.DataBase.Configurations;


public class SafetyEventConfiguration : IEntityTypeConfiguration<SafetyEvent>
{
    public void Configure(EntityTypeBuilder<SafetyEvent> builder)
    {
        builder.ToTable("SafetyEvents");
        
        builder.HasKey(e => e.Id);

        // the SafteyEvent Id is generated outside the database From the MessageId of SQS
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.Message)
               .HasMaxLength(500);

        builder.Property(e => e.AlertLevel)
           .HasConversion(
                 convertToProviderExpression: (AlertLevel) => AlertLevel.ToString(),
                convertFromProviderExpression: (_alertLevel) => (AlertLevel)Enum.Parse(typeof(AlertLevel), _alertLevel)
            ) 
           .HasMaxLength(20);


        
        builder.Property(e => e.DriverState)
               .HasConversion(
                         convertToProviderExpression: (DriverState) => DriverState.ToString(),
                         convertFromProviderExpression: (_driverState) => (DriverState)Enum.Parse(typeof(DriverState), _driverState)
                ) 
               .HasMaxLength(50);


        // Indexes for Analytics
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.AlertLevel);
        builder.HasIndex(e => e.DriverId);

        // Relationships
        builder.HasOne(e => e.Driver)
               .WithMany(d => d.SafetyEvents)
               .HasForeignKey(e => e.DriverId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Vehicle)
               .WithMany(v => v.SafetyEvents)
               .HasForeignKey(e => e.VehicleId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
