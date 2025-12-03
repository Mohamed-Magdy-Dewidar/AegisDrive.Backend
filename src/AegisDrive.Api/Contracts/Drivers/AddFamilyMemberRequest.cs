namespace AegisDrive.Api.Contracts.Drivers;




public record AddFamilyMemberRequest(string FullName, string PhoneNumber, string Email, string? Relationship, bool NotifyOnCritical);

