namespace AegisDrive.Api.Contracts.Vehicles;

public record GetVehicleResponse(int Id,string PlateNumber,string? Model,string Status,int? CurrentDriverId,int? CompanyId , string? OwnerUserId);
