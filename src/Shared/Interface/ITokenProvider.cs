using Examination.Api.Entities;

namespace Shared.Interface
{
    public interface ITokenProvider
    {
        public Task<string> CreateTokenAsync(ApplicationUser user);
    }
}
