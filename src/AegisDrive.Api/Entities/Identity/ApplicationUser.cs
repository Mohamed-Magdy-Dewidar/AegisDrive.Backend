using Microsoft.AspNetCore.Identity;
namespace AegisDrive.Api.Entities.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;




    // The Multi-Tenancy Key
    // NULL = Individual User
    // VALUE = Company Employee/Manager
    public int? CompanyId { get; set; }


    public  Company? Company { get; set; }


}