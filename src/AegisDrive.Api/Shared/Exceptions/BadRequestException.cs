namespace AegisDrive.Api.Shared.Exceptions;

public class BadRequestException : Exception
{
    public Dictionary<string, string[]>? Errors { get; set; }

    public BadRequestException(string message) : base(message) { }

    public BadRequestException(string message, Dictionary<string, string[]> errors) : base(message)
    {
        Errors = errors;
    }
}