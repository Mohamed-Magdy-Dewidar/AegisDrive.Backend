namespace AegisDrive.Api.Contracts.Drivers;

public record RegisterDriverRequest(string FullName, string PhoneNumber, string Email, IFormFile Image,  string? CompanyId);
