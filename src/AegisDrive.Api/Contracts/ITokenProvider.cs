using AegisDrive.Api.Entities.Identity;
namespace AegisDrive.Api.Contracts;

public interface ITokenProvider
{
    public Task<string> CreateTokenAsync(ApplicationUser user);

}
