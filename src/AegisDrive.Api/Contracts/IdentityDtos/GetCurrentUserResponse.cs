namespace AegisDrive.Api.Contracts.IdentityDtos;

public record GetCurrentUserResponse(
        string Id,
        string FullName,
        string Email,
        string Role,
        string? AvatarUrl,
        int? DriverId,     // For linking to Driver Profile
        int? CompanyId     // For linking to Company Dashboard
    );