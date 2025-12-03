using Azure.Core;

namespace AegisDrive.Api.Entities;

public class FamilyMember : BaseEntity<int>
{

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; }
    public string? Relationship { get; set; }
    public bool NotifyOnCritical { get; set; } = true;

    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;


    public FamilyMember(int driverId,string fullName,string phoneNumber,string email,string? relationship,bool notifyOnCritical = true)
    {
        DriverId = driverId;
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        Relationship = relationship;
        NotifyOnCritical = notifyOnCritical;
    }


}
