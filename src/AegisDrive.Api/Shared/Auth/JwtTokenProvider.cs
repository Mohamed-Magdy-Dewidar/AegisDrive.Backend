using AegisDrive.Api.Contracts;
using AegisDrive.Api.Entities.Identity;
using Microsoft.AspNetCore.Identity;
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

    public JwtTokenProvider(IOptions<JwtSettings> jwtSettingsOptions, UserManager<ApplicationUser> userManager)
    {
        _jwtSettings = jwtSettingsOptions.Value;
        _userManager = userManager;
    }


    public async Task<string> CreateTokenAsync(ApplicationUser user)
    {
        // 1. Standard Claims (Sub, Email, JTI)
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique Token ID
            new(AuthConstants.Claims.FullName, user.FullName)
        };

        // 2. Add Roles (Fetch from DB)
        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // 3. AUTO-ASSIGN ROLE if missing (Fallback Logic)
        // If the DB doesn't have a role yet, derive it from the user state
        if (!roles.Any())
        {
            string derivedRole = user.CompanyId.HasValue
                ? AuthConstants.Roles.Manager
                : AuthConstants.Roles.Individual;

            claims.Add(new Claim(ClaimTypes.Role, derivedRole));
        }

        // 4. CRITICAL: Add Company Context
        // This acts as the "Passport Stamp" allowing them to see Company Data
        if (user.CompanyId.HasValue)
        {
            claims.Add(new Claim(AuthConstants.Claims.CompanyId, user.CompanyId.Value.ToString()));
        }

        // 5. Sign & Build
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
