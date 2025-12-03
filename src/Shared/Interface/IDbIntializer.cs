namespace Shared.Interface
{
    public interface IDbIntializer
    {
        Task SeedDataAsync();
        
        Task IdentitySeedDataAsync();
    }
}
