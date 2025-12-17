namespace AegisDrive.Api.Contracts.Drivers;


public record ListDriversResponse(string FullName,string PhoneNumber,string? Email,bool IsActive,int SafetyScore,string? CompanyName)
{
    public string? PictureUrl { get; set; }
}


