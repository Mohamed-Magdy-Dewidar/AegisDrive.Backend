namespace AegisDrive.Api.Contracts.Drivers;

public record GetDriverFamilyMembersResponse(string FullName, string PhoneNumber, string Email, string? Relationship, bool NotifyOnCritical, int DriverId);


