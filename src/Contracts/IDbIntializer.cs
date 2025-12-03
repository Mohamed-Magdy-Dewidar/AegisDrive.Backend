namespace Contracts;

public interface IDbIntializer
{
    Task MigrateAsync();
    Task SeedDataAsync();

}
