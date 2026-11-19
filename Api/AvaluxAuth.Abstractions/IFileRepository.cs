namespace AvaluxAuth.Abstractions;

public interface IFileRepository
{
    public Task<Stream> DownloadFileAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default);

    public Task<Stream> DownloadFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default);

    public Task DeleteFileAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default);

    public Task DeleteFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default);

    public Task<bool> FileExistsAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default);

    public Task<bool> FileExistsAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default);

    public Task UploadFileAsync(FileRepositoryBucket bucket, Guid fileId, Stream fileStream,
        CancellationToken ct = default);

    public Task UploadFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName, Stream fileStream,
        CancellationToken ct = default);

    public Task<string> GetDownloadUrlAsync(FileRepositoryBucket bucket, Guid fileId, TimeSpan timeout,
        CancellationToken ct = default);

    public Task<string> GetDownloadUrlAsync(FileRepositoryBucket bucket, Guid fileId, string fileName, TimeSpan timeout,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes every object older than <paramref name="createdBefore"/> whose file id
    /// is not in <paramref name="keepFileIds"/> and returns the number of deleted
    /// objects. M-01 in SECURITY-AUDIT.md: orphaned uploads (abandoned sign-ups,
    /// avatars replaced in the profile) must not accumulate in the bucket.
    /// </summary>
    public Task<int> ClearUnreferencedFilesAsync(FileRepositoryBucket bucket, DateTime createdBefore,
        IReadOnlySet<Guid> keepFileIds, CancellationToken ct = default);
}

public enum FileRepositoryBucket
{
    Avatars
}