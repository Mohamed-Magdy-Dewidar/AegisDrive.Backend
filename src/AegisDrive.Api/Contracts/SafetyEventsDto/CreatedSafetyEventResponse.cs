namespace AegisDrive.Api.Contracts.SafetyEventsDto;

public record CreatedSafetyEventResponse(string Id , string? DriverImageKey)
{
    public string Message { get; init; } = "Safety Event logged successfully.";
}