namespace AegisDrive.Api.Contracts.Users;

public record RegisterRequest(
        string Email,
        string Password,
        string FullName,
        string AccountType,
        string? CompanyName
    );

