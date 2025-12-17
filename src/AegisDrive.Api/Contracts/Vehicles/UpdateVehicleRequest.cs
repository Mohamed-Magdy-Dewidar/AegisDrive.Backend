using AegisDrive.Api.Entities.Enums;

namespace AegisDrive.Api.Contracts.Vehicles;

public record UpdateVehicleRequest(int VehicleId, string PlateNumber, string Model, VehicleStatus Status);
