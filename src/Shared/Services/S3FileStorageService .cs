using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Shared.Interface;
using System; // For Exception
using System.IO; // For Path

namespace Examination.Api.Shared.Services
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

        public async Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = _s3Settings.BucketName,
                    Key = key
                };

                var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
                return new StoredFile(response.ResponseStream, response.Headers.ContentType);
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // This is a special, expected case. We handle it by returning null,
                // making the service easier for the handler to use.
                return null;
            }
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
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _s3Settings.BucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(15),
            };

            return _s3Client.GetPreSignedURL(request);
        }
    }
}