using System.Diagnostics;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using AvaluxAuth.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AvaluxAuth.S3;

public class S3Repository(ILogger<S3Repository> logger, IConfiguration configuration) : IFileRepository
{
    private readonly AmazonS3Client _s3Client = new(
        new BasicAWSCredentials(configuration["S3.AccessKey"],
            configuration["S3.SecretKey"]),
        new AmazonS3Config
        {
            ServiceURL = configuration["S3.ServiceUrl"],
            AuthenticationRegion = configuration["S3.AuthorizationRegion"],
            ForcePathStyle = true,
            Timeout = TimeSpan.FromSeconds(2),
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = 0,
            ConnectTimeout = TimeSpan.FromSeconds(2),
        });

    private readonly AmazonS3Client _retryS3Client = new(
        new BasicAWSCredentials(configuration["S3.AccessKey"],
            configuration["S3.SecretKey"]),
        new AmazonS3Config
        {
            ServiceURL = configuration["S3.ServiceUrl"],
            AuthenticationRegion = configuration["S3.AuthorizationRegion"],
            ForcePathStyle = true,
            Timeout = TimeSpan.FromSeconds(15),
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = 3,
            ConnectTimeout = TimeSpan.FromSeconds(2),
        });

    private async Task<TResult> RetryRequest<TResult>(Func<AmazonS3Client, Task<TResult>> action,
        CancellationToken ct = default)
    {
        try
        {
            return await action(_s3Client);
        }
        catch (TimeoutException)
        {
            ct.ThrowIfCancellationRequested();
            return await action(_retryS3Client);
        }
    }

    public Task<Stream> DownloadFileAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default)
    {
        return DownloadFileAsync(GetBucket(bucket), fileId.ToString(), ct);
    }

    public Task<Stream> DownloadFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default)
    {
        return DownloadFileAsync(GetBucket(bucket), $"{fileId.ToString()}/{fileName}", ct);
    }

    public Task DeleteFileAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default)
    {
        return DeleteFileAsync(GetBucket(bucket), fileId.ToString(), ct);
    }

    public Task DeleteFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default)
    {
        return DeleteFileAsync(GetBucket(bucket), $"{fileId.ToString()}/{fileName}", ct);
    }

    public Task<bool> FileExistsAsync(FileRepositoryBucket bucket, Guid fileId, CancellationToken ct = default)
    {
        return FileExistsAsync(GetBucket(bucket), fileId.ToString(), ct);
    }

    public Task<bool> FileExistsAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        CancellationToken ct = default)
    {
        return FileExistsAsync(GetBucket(bucket), $"{fileId.ToString()}/{fileName}", ct);
    }

    public Task UploadFileAsync(FileRepositoryBucket bucket, Guid fileId, Stream fileStream,
        CancellationToken ct = default)
    {
        return UploadFileAsync(GetBucket(bucket), fileId.ToString(), fileStream, ct);
    }

    public Task UploadFileAsync(FileRepositoryBucket bucket, Guid fileId, string fileName, Stream fileStream,
        CancellationToken ct = default)
    {
        return UploadFileAsync(GetBucket(bucket), $"{fileId.ToString()}/{fileName}", fileStream, ct);
    }

    public Task<string> GetDownloadUrlAsync(FileRepositoryBucket bucket, Guid fileId, TimeSpan timeout,
        CancellationToken ct = default)
    {
        return GetDownloadUrlAsync(GetBucket(bucket), fileId.ToString(), timeout, ct);
    }

    public async Task<string> GetDownloadUrlAsync(FileRepositoryBucket bucket, Guid fileId, string fileName,
        TimeSpan timeout, CancellationToken ct = default)
    {
        if (!await FileExistsAsync(bucket, fileId, fileName, ct))
            return await GetDownloadUrlAsync(GetBucket(bucket), $"{fileId}{Path.GetExtension(fileName)}", timeout, ct);
        return await GetDownloadUrlAsync(GetBucket(bucket), $"{fileId.ToString()}/{fileName}", timeout, ct);
    }

    private async Task<Stream> DownloadFileAsync(string bucket, string fileName, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var stream = (await RetryRequest(client => client.GetObjectAsync(bucket, fileName, ct), ct))
            .ResponseStream;
        stopwatch.Stop();
        logger.LogInformation("Object '{name}' downloaded from '{bucket}' in {time}.'", fileName, bucket,
            stopwatch.Elapsed);
        return stream;
    }

    private async Task DeleteFileAsync(string bucket, string fileName, CancellationToken ct = default)
    {
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = fileName,
        };

        var stopwatch = Stopwatch.StartNew();
        await RetryRequest(client => client.DeleteObjectAsync(deleteRequest, ct), ct);
        stopwatch.Stop();
        logger.LogInformation("Object '{name}' deleted from '{bucket}' in {time}.'", fileName, bucket,
            stopwatch.Elapsed);
    }

    private async Task<bool> FileExistsAsync(string bucket, string fileName, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = fileName,
            };

            await RetryRequest(client => client.GetObjectMetadataAsync(request, ct), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task UploadFileAsync(string bucket, string fileName, Stream fileStream,
        CancellationToken ct = default)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = fileName,
            InputStream = fileStream,
            ContentType = "application/octet-stream"
        };

        var stopwatch = Stopwatch.StartNew();
        await _retryS3Client.PutObjectAsync(putRequest, ct);
        stopwatch.Stop();
        logger.LogInformation("Object '{name}' uploaded to '{bucket}' in {time}.'", fileName, bucket,
            stopwatch.Elapsed);
    }

    private async Task<string> GetDownloadUrlAsync(string bucket, string fileName, TimeSpan timeout,
        CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fileName,
            Expires = DateTime.UtcNow.Add(timeout)
        };
        var stopwatch = Stopwatch.StartNew();
        var url = await RetryRequest(client => client.GetPreSignedURLAsync(request), ct);
        stopwatch.Stop();
        logger.LogInformation("Url for object '{name}' in '{bucket}' get in {time}", fileName, bucket,
            stopwatch.Elapsed);
        return url;
    }

    public async Task<int> ClearFilesCreatedBefore(FileRepositoryBucket bucket, DateTime beforeDate,
        CancellationToken ct = default)
    {
        var count = 0;
        var bucketName = GetBucket(bucket);
        var files = await RetryRequest(client => client.ListObjectsAsync(bucketName, ct), ct);
        foreach (var file in files?.S3Objects ?? [])
        {
            if (file != null && file.LastModified < beforeDate)
            {
                await DeleteFileAsync(bucketName, file.Key, ct);
                count++;
            }
        }

        return count;
    }

    private string AvatarsBucket { get; } = configuration["S3.Buckets.Avatars"] ?? "avatars";

    private string GetBucket(FileRepositoryBucket bucket)
    {
        return bucket switch
        {
            FileRepositoryBucket.Avatars => AvatarsBucket,
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null)
        };
    }
}