namespace AegisDrive.Api.Contracts;

public interface IDbIntializer
{
    Task MigrateAsync();
    Task SeedDataAsync();

    Task IdentitySeedDataAsync();
}
