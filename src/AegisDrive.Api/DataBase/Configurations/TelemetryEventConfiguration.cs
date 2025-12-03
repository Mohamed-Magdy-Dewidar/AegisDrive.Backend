using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AegisDrive.API.DataBase.Configurations;

public class TelemetryEventConfiguration : IEntityTypeConfiguration<TelemetryEvent>
{
    public void Configure(EntityTypeBuilder<TelemetryEvent> builder)
    {
        builder.ToTable("TelemetryEvents");
        builder.HasKey(t => t.Id); // BigInt


        builder.Property(t => t.DeviceId).HasMaxLength(50);


        // Indexes for Time-Series queries
        builder.HasIndex(t => t.Timestamp);
        builder.HasIndex(t => t.VehicleId);
        builder.HasIndex(t => t.EventType);

        builder.HasOne(t => t.Vehicle)
               .WithMany(v => v.TelemetryEvents)
               .HasForeignKey(t => t.VehicleId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}