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

    public Task<int> ClearFilesCreatedBefore(FileRepositoryBucket bucket, DateTime beforeDate,
        CancellationToken ct = default);
}

public enum FileRepositoryBucket
{
    Avatars
}