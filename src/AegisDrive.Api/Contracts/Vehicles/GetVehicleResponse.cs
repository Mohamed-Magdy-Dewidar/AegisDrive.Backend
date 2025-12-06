namespace AegisDrive.Api.Features.Fleet;

public static partial class GetVehicle
{
    public record GetVehicleResponse(int Id,string PlateNumber,string? Model,string Status,int? CurrentDriverId,int? CompanyId);
}