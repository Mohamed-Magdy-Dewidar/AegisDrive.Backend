namespace AegisDrive.Api.Contracts.Drivers;


public record DeleteDriverResponse(bool Success)
{
    public string Message { get; init; } = "Driver Profile deleted successfully.";
}