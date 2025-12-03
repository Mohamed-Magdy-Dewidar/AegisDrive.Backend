using AegisDrive.Api.Entities.Enums.Device;
using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace AegisDrive.Api.DataBase.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");

        builder.HasKey(d => d.Id); // String ID (Serial Number)


        // will be assigned externally, e.g., by the device manufacturer EX(RPI-XYZ)
        builder.Property(d => d.Id)
            .ValueGeneratedNever();


        // Store Type as string ("RaspberryPi", "Esp32")
        builder.Property(d => d.Type)
               .HasConversion(
                     convertToProviderExpression: (DeviceType) => DeviceType.ToString(),
                     convertFromProviderExpression: (_type) => (DeviceType)Enum.Parse(typeof(DeviceType), _type)
            ).HasMaxLength(50)
             .IsRequired();


        // Store Status as string ("Online", "Offline")
        builder.Property(d => d.Status)
               .HasConversion(
                        convertToProviderExpression: (DeviceStatus) => DeviceStatus.ToString(),
                        convertFromProviderExpression: (_status) => (DeviceStatus)Enum.Parse(typeof(DeviceStatus), _status)
            ).
            HasMaxLength(50);
        
        
        builder.Property(d => d.Id).HasMaxLength(50);
        builder.Property(d => d.Type).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Status).HasMaxLength(20);
        builder.Property(d => d.FirmwareVersion).HasMaxLength(20);

        builder.HasOne(d => d.Vehicle)
               .WithMany(v => v.Devices)
               .HasForeignKey(d => d.VehicleId)
               .OnDelete(DeleteBehavior.SetNull);
    }

  
}
