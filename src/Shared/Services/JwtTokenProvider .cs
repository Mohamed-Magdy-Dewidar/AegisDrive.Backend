using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Examination.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Interface;

namespace Examination.Api.Shared.Services
{
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
            
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id), 
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Name, user.UserName!)
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
            

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
}