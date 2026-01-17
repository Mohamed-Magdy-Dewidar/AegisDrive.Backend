using AegisDrive.Api.Shared.ResultEndpoint;
using MediatR;

namespace AegisDrive.Api.Contracts.Vehicles;

public record ListVehiclesResponse(int Id , string PlateNumber , string? Model, string Status , string? CurrentDriverName);


