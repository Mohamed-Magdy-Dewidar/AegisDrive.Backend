namespace AegisDrive.Api.Contracts.Drivers;


public record RegisterDriverResponse(int Id)
{
    public string Message { get; init; } = "Driver Created Succesfully";
}
