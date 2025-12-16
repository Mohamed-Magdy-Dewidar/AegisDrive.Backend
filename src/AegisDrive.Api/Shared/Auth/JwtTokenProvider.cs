using AegisDrive.Api.Contracts;
using AegisDrive.Api.DataBase;
using AegisDrive.Api.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AegisDrive.Api.Shared.Auth;

public class JwtTokenProvider : ITokenProvider
{
    private readonly JwtSettings _jwtSettings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;

    public JwtTokenProvider(IOptions<JwtSettings> jwtSettingsOptions,UserManager<ApplicationUser> userManager,AppDbContext dbContext)
    {
        _jwtSettings = jwtSettingsOptions.Value;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<string> CreateTokenAsync(ApplicationUser user)
    {
        // 1. Standard Claims
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AuthConstants.Claims.FullName, user.FullName)
        };

        // 2. Roles
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 3. Auto-Assign Role (Fallback)
        if (!roles.Any())
        {
            string derivedRole = user.CompanyId.HasValue
                ? AuthConstants.Roles.Manager
                : AuthConstants.Roles.Individual;

            claims.Add(new Claim(ClaimTypes.Role, derivedRole));
            roles.Add(derivedRole);
        }


        // =================================================================
        // 4. Resolve DriverId via Vehicle Ownership
        // =================================================================
        if (roles.Contains(AuthConstants.Roles.Individual))
        {
            // Since Driver has no FK to User, we go through the Vehicle.
            // Logic: Find the vehicle owned by this user, and get who is driving it.
            var driverId = await _dbContext.Vehicles
                .AsNoTracking()
                .Where(v => v.OwnerUserId == user.Id && v.CurrentDriverId.HasValue)
                .Select(v => v.CurrentDriverId)
                .FirstOrDefaultAsync();

            if (driverId.HasValue)
            {
                claims.Add(new Claim(AuthConstants.Claims.DriverId, driverId.Value.ToString()));
            }
        }
        
        // 5. Company Context
        if (user.CompanyId.HasValue)
        {
            claims.Add(new Claim(AuthConstants.Claims.CompanyId, user.CompanyId.Value.ToString()));
        }

        // 6. Sign & Build
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(_jwtSettings.ExpirationInDays);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}