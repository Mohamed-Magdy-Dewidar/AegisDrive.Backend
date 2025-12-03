namespace Shared;



public sealed class S3Settings
{
    public const string SectionName = "S3Settings";
    public string Region { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
}