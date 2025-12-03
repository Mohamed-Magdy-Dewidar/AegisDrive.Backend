namespace Shared.Interface
{
    // A simple record to hold the retrieved file's data
    public record StoredFile(Stream FileStream, string ContentType);

    public  interface IFileStorageService
    {
        /// <summary>
        /// Uploads a file from an IFormFile object to the storage provider.
        /// </summary>
        /// <param name="file">The IFormFile to upload.</param>
        /// <param name="prefix">the location of file in the storage Provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The unique key assigned to the stored file.</returns>
        Task<string> UploadAsync(IFormFile file, string prefix , CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Retrieves a file from the storage provider.
        /// </summary>
        /// <param name="key">The unique key of the file to retrieve.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A StoredFile record containing the stream and content type, or null if not found.</returns>
        Task<StoredFile?> GetAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from the storage provider.
        /// </summary>
        /// <param name="key">The unique key of the file to delete.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);


        /// <summary>
        /// Generates a temporary, pre-signed URL to access a private file.
        /// </summary>
        /// <param name="key">The unique key of the file.</param>
        /// <returns>A full, time-limited URL for the file.</returns>
        string GetPresignedUrl(string key);


    }
}