using AegisDrive.Api.Contracts;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;


namespace AegisDrive.Api.Shared.Services
{
    public  class S3FileStorageService : IFileStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly S3Settings _s3Settings;

        public S3FileStorageService(IAmazonS3 s3Client, IOptions<S3Settings> s3Settings)
        {
            _s3Client = s3Client;
            _s3Settings = s3Settings.Value;
        }

        public async Task<string> UploadAsync(IFormFile file, string prefix, CancellationToken cancellationToken = default)
        {
            // No try-catch here. If upload fails, the original, detailed exception should
            var fileExtension = Path.GetExtension(file.FileName);
            var key = $"{prefix}/{Guid.NewGuid()}{fileExtension}";

            using var stream = file.OpenReadStream();

            var putRequest = new PutObjectRequest
            {
                BucketName = _s3Settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                Metadata = { ["original-file-name"] = file.FileName }
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            return key;
        }

       

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            // No try-catch here. Let unexpected exceptions bubble up.
            // S3 does not throw an error if the key doesn't exist, so we don't
            // need to handle the "NoSuchKey" case.
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _s3Settings.BucketName,
                Key = key
            };
            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
        }

        public string GetPresignedUrl(string key)
        {
            // No try-catch here. If URL generation fails, it's an unexpected error
            // and the handler needs to know about it. Returning "" or null would hide the problem.
            if(string.IsNullOrEmpty(key))
                return string.Empty;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _s3Settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(_s3Settings.PresignedUrlExpiryMinutes),
            };

            return _s3Client.GetPreSignedURL(request);
        }

        public IDictionary<string, string> GetPresignedUrls(IEnumerable<string> keys)
        {
            var results = new Dictionary<string, string>();
            var expiry = DateTime.UtcNow.AddMinutes(_s3Settings.PresignedUrlExpiryMinutes);
            foreach(var key in keys)
            {
                if (string.IsNullOrEmpty(key) || results.ContainsKey(key)) continue;

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key,
                    Verb = HttpVerb.GET,
                    Expires = expiry,
                };
                string preSignedURL = _s3Client.GetPreSignedURL(request);
                results[key] = preSignedURL;
            }
            return results;
        }

        public async Task<string> MoveFileAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
        {
            // 1. Copy
            var copyRequest = new CopyObjectRequest
            {
                SourceBucket = _s3Settings.BucketName,
                SourceKey = sourceKey,
                DestinationBucket = _s3Settings.BucketName,
                DestinationKey = destinationKey
            };
            await _s3Client.CopyObjectAsync(copyRequest, cancellationToken);

            // 2. Delete Original
            await DeleteAsync(sourceKey, cancellationToken);

            return destinationKey;
        }
    }
}