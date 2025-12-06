
namespace AegisDrive.Api.Shared;

public sealed class SqsSettings
{

    public const string SectionName = nameof(SqsSettings);
    
    public string DrowsinessCriticalEventsQueueUrl { get; init; } = string.Empty;
    public string DrowsinessEventsQueueUrl { get; init; } = string.Empty;
  
}
