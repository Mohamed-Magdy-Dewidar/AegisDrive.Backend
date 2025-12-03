using AegisDrive.Api.Entities;
using Microsoft.EntityFrameworkCore;


namespace AegisDrive.Api.DataBase.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    

    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.RepresentativeName).HasMaxLength(100);
        builder.Property(c => c.RepresentativeEmail).HasMaxLength(100);
        builder.Property(c => c.RepresentativePhone).HasMaxLength(20);



        // Configure Id to not be auto-generated        
        //builder.Property(p => p.Id)
        //    .ValueGeneratedNever();


        // A Company has many Drivers. If Company is deleted, keep Drivers but set CompanyId to NULL (Restrict).
        builder.HasMany(c => c.Drivers)
               .WithOne(d => d.Company)
               .HasForeignKey(d => d.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Vehicles)
               .WithOne(v => v.Company)
               .HasForeignKey(v => v.CompanyId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
