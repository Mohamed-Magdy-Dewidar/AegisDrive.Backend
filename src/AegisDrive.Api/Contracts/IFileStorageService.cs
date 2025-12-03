namespace AegisDrive.Api.Contracts
{
    
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


        /// <summary>
        /// Generates a temporary, pre-signed URLs to access a privates files.
        /// </summary>
        /// <param name="key">The unique keys of the files.</param>
        /// <returns>A full Hashmap where the key is the path in the Db and the value is the actual time based PresignedUrls</returns>
        public IDictionary<string, string> GetPresignedUrls(IEnumerable<string> keys);


    }
}