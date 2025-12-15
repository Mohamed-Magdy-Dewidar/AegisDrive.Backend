namespace AegisDrive.Api.Contracts.Users;

public record LoginResponse(string Token, string FullName, string Role, int? CompanyId);

