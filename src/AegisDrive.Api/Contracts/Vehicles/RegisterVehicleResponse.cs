namespace AegisDrive.Api.Contracts.Vehicles;

public record RegisterVehicleResponse(int Id)
{
    public string Message { get; init; } = "Vehicle registered successfully.";
}
