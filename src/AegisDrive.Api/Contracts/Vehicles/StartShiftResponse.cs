namespace AegisDrive.Api.Contracts.Vehicles;

public record StartShiftResponse(long AssignmentId, int DriverId, int VehicleId, DateTime AssignedAtUtc, string Message);