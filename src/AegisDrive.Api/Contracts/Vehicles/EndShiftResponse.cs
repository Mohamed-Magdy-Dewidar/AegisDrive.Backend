namespace AegisDrive.Api.Contracts.Vehicles;

public record EndShiftResponse(
        long AssignmentId,
        int DriverId,
        int VehicleId,
        DateTime StartTime,
        DateTime EndTime,
        string Duration)
{
    public string Message { get; init; } = $"Shift ended successfully. Duration: {Duration}";
}