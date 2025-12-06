using System.Collections;

namespace AegisDrive.Api.Contracts.Drivers;

public record GetDriverProfileResponse(string FullName , string PhoneNumber , string? Email , bool IsActive , int SafteyScore  , CompanyDto? DriverCompany , IEnumerable<FamilyMemberDto> DriverFamilyMembers) 
{
    public string? PictureUrl { get; set; } = string.Empty;
}


public record CompanyDto(string Name , string? RepresentativeName , string? RepresentativeEmail ,  string? RepresentativePhone);

public record FamilyMemberDto(string FullName , string PhoneNumber , string Email, string? Relationship , bool NotifyOnCritical = true);

