namespace AegisDrive.Api.Contracts.Trips;

// Simple DTO for the Start Request
public record StartTripRequest(
    int VehicleId,
    string DestinationText,
    decimal DestinationLat,
    decimal DestinationLng
);