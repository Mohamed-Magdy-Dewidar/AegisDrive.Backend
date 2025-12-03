using System.ComponentModel.DataAnnotations.Schema;

namespace AegisDrive.Api.Entities;

public class Driver : BaseEntity<int>
{

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public int SafetyScore { get; set; } = 100;
    
    
    public string? PictureUrl { get; set; }




    public Company? Company { get; set; }
    
    public int? CompanyId { get; set; }



    [NotMapped]
    public bool BelongsToACompany => CompanyId != null;


    public ICollection<VehicleAssignment> VehicleAssignments { get; set; } = new List<VehicleAssignment>();
    public ICollection<SafetyEvent> SafetyEvents { get; set; } = new List<SafetyEvent>();
    public ICollection<FamilyMember> FamilyMembers { get; set; } = new List<FamilyMember>();


    public Driver(string FullName, string Email, string PhoneNumber, string? PictureUrl, int? CompanyId)
    {
        this.FullName = FullName;
        this.Email = Email;
        this.PhoneNumber = PhoneNumber;
        this.CompanyId = CompanyId;


        this.PictureUrl = PictureUrl;
        if (string.IsNullOrEmpty(PictureUrl))
            this.PictureUrl = "";
    

    }


}
