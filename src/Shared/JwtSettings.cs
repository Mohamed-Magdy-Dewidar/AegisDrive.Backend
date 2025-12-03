namespace Shared;


public class JwtSettings
{
    public const string SectionName = "JWT";

    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpirationInDays { get; init; }

}
