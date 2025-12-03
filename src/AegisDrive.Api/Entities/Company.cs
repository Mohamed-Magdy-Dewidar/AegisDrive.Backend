namespace AegisDrive.Api.Entities;


public class Company : BaseEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? RepresentativeName { get; set; }
    public string? RepresentativeEmail { get; set; }
    public string? RepresentativePhone { get; set; }
    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
