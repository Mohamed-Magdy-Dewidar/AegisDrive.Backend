namespace AegisDrive.Api.DataBase.Configurations;

using AegisDrive.Api.Entities;
using AegisDrive.Api.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.ToTable("Trips");

        builder.HasKey(t => t.Id);


        // 📍 Precision for GPS Coordinates (Latitude/Longitude)
        // 18 total digits, 10 after the decimal point
        builder.Property(t => t.StartLat).HasPrecision(18, 10);
        builder.Property(t => t.StartLng).HasPrecision(18, 10);
        builder.Property(t => t.DestinationLat).HasPrecision(18, 10);
        builder.Property(t => t.DestinationLng).HasPrecision(18, 10);

        // Optional: If you also added EndLat/EndLng in your previous step
        builder.Property(t => t.EndLat).HasPrecision(18, 10);
        builder.Property(t => t.EndLng).HasPrecision(18, 10);

        // 📝 Text and JSON storage
        builder.Property(t => t.DestinationText).HasMaxLength(500);
        builder.Property(t => t.RouteGeometryJson).IsRequired(false);

        // ⚙️ Enum Conversion (Stores the name of the status instead of an int for readability)
        builder.Property(t => t.Status)
            .HasConversion(
                convertToProviderExpression: (status) => status.ToString(),
                convertFromProviderExpression: (_status) => (TripStatus)Enum.Parse(typeof(TripStatus), _status)
            )
            .HasMaxLength(50);


        // 🔗 Relationships

        // A Driver can have many Trips. 
        // If a Driver is deleted, we Restrict to avoid orphan Trip data.
        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        // A Vehicle can have many Trips.
        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(t => t.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}