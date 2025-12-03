using System.ComponentModel.DataAnnotations.Schema;

namespace AegisDrive.Api.Entities;

public class VehicleAssignment : BaseEntity<long>
{
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnassignedAt { get; set; }



    
    public Driver Driver { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}
