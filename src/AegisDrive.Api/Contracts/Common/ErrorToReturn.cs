namespace AegisDrive.Api.Contracts.Common;

public class ErrorToReturn
{
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Optional: List of validation errors (e.g., "Email is required", "Password too short")
    public Dictionary<string, string[]>? Errors { get; set; }
}