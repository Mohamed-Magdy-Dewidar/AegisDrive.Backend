namespace AegisDrive.Api.Contracts.Drivers;


public record ListDriversResponse
    (int DriverId , string FullName,string PhoneNumber,string? Email,bool IsActive,int SafetyScore,int? companyId , string? CompanyName)
{
    public string? PictureUrl { get; set; }
}


