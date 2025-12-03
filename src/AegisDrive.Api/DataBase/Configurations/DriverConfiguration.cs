namespace AegisDrive.Api.DataBase.Configurations;

using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");


        builder.HasKey(d => d.Id);
 

        builder.Property(d => d.FullName).IsRequired().HasMaxLength(100);
        builder.Property(d => d.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Email).HasMaxLength(100);


        builder.HasIndex(d => d.Email).IsUnique();
        builder.HasIndex(d => d.PhoneNumber).IsUnique();


    }
}
