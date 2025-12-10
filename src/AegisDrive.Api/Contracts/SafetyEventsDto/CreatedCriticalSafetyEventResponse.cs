namespace AegisDrive.Api.Contracts.SafetyEventsDto;

public record CreatedCriticalSafetyEventResponse(string Id,string? DriverImageKey,string? RoadImageKey)
{
    public string Message { get; init; } = "Safety Event Logged successfully.";
}
