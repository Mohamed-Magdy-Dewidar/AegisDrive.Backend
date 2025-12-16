using AegisDrive.Api.Contracts;
using AegisDrive.Api.Contracts.IdentityDtos;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities.Identity;
using AegisDrive.Api.Shared;
using AegisDrive.Api.Shared.Auth;
using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace AegisDrive.Api.Features.Users.Identity;

public static class GetCurrentUser
{
    
    public record Query(string UserId) : IRequest<Result<GetCurrentUserResponse>>;

    internal sealed class Handler : IRequestHandler<Query, Result<GetCurrentUserResponse>>
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _dbContext;
        private readonly IDatabase _redis;
        private readonly IFileStorageService _fileService;

        public Handler(UserManager<ApplicationUser> userManager,AppDbContext dbContext,IConnectionMultiplexer redis,IFileStorageService fileService)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _redis = redis.GetDatabase();
            _fileService = fileService;   
        }

        public async Task<Result<GetCurrentUserResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // A. CACHE CHECK
            var cacheKey = $"user:profile:{request.UserId}";
            var cachedProfile = await _redis.StringGetAsync(cacheKey);

            if (!cachedProfile.IsNull)
            {
                var cachedResponse = JsonSerializer.Deserialize<GetCurrentUserResponse>(cachedProfile!);
                return Result<GetCurrentUserResponse>.Success(cachedResponse!);
            }

            // B. DATABASE FETCH (Cache Miss)
            var appUser = await _userManager.FindByIdAsync(request.UserId);
            if (appUser == null)
            {
                return Result<GetCurrentUserResponse>.Failure<GetCurrentUserResponse>(
                    new Error("User.NotFound", "User not found"));
            }

            var role = (await _userManager.GetRolesAsync(appUser)).FirstOrDefault() ?? AuthConstants.Roles.Individual;

            int? driverId = null;
            string? avatarUrl = null;

            // C. RESOLVE DRIVER DATA (Optimized)
            if (role == AuthConstants.Roles.Individual)
            {
                var driverData = await _dbContext.Vehicles
                    .AsNoTracking()
                    .Where(v => v.OwnerUserId == request.UserId && v.CurrentDriverId.HasValue)
                    .Select(v => new
                    {
                        v.CurrentDriverId,
                        ImageKey = v.CurrentDriver != null ? v.CurrentDriver.PictureUrl : null
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (driverData != null)
                {
                    driverId = driverData.CurrentDriverId;

                    if (!string.IsNullOrEmpty(driverData.ImageKey))
                    {
                        avatarUrl = _fileService.GetPresignedUrl(driverData.ImageKey);
                    }
                }
            }

           
            var response = new GetCurrentUserResponse(
                appUser.Id,
                appUser.FullName,
                appUser.Email!,
                role,
                avatarUrl,
                driverId,
                appUser.CompanyId
            );

            // E. CACHE SET (1 Hour TTL)
            await _redis.StringSetAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                TimeSpan.FromHours(1));

            return Result<GetCurrentUserResponse>.Success(response);
        }
    }

  
}