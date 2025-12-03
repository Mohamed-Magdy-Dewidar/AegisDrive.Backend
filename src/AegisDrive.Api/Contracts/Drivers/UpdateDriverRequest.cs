namespace AegisDrive.Api.Contracts.Drivers;



public record UpdateDriverRequest(int DriverId ,  string? FullName , string? PhoneNumber, string? Email, string? CompanyId);