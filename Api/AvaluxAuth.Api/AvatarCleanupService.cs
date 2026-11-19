using AvaluxAuth.Abstractions;

namespace AvaluxAuth.Api;

/// <summary>
/// M-01 in SECURITY-AUDIT.md: avatar uploads were anonymous and never removed,
/// so the bucket could be used as free image hosting. Uploads now require a live
/// authorization state and are quota-limited, but a user can still upload and
/// then abandon the flow, or replace an avatar in the profile. This service
/// deletes such orphans: objects older than <c>AvatarCleanup:MinAge</c> that
/// no account references any more.
/// </summary>
/// <remarks>
/// Runs in every instance, which is safe because deleting the same orphan twice
/// is idempotent. The initial delay keeps the startup path (EF migrations, Redis
/// connect) from competing with an S3 listing on a cold start.
/// </remarks>
public class AvatarCleanupService(
    IServiceScopeFactory scopeFactory,
    IFileRepository fileRepository,
    IConfiguration configuration,
    ILogger<AvatarCleanupService> logger) : BackgroundService
{
    // The URL written into Accounts.AvatarUrl and exposed by the auth UI.
    private const string AvatarPath = "/api/v1/avatar/";

    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = GetTimeSpanSetting("AvatarCleanup:Interval", TimeSpan.FromHours(24), TimeSpan.FromDays(7));
        var minAge = GetTimeSpanSetting("AvatarCleanup:MinAge", TimeSpan.FromHours(24), TimeSpan.FromDays(30));
        logger.LogInformation("Avatar cleanup: interval {Interval}, min age {MinAge}", interval, minAge);

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await CleanupAsync(minAge, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // A transient S3/DB failure must not kill the hosted service.
                logger.LogError(e, "Avatar cleanup failed; retrying in {Interval}", interval);
            }
        } while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private async Task CleanupAsync(TimeSpan minAge, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var passwordRepository = scope.ServiceProvider.GetRequiredService<IPasswordRepository>();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var keepFileIds = new HashSet<Guid>(await passwordRepository.GetAvatarIdsAsync(ct));
        foreach (var avatarUrl in await accountRepository.GetAvatarUrlsAsync(AvatarPath, ct))
        {
            if (TryParseAvatarId(avatarUrl, out var avatarId))
                keepFileIds.Add(avatarId);
        }

        var createdBefore = DateTime.UtcNow - minAge;
        var deleted = await fileRepository.ClearUnreferencedFilesAsync(FileRepositoryBucket.Avatars, createdBefore,
            keepFileIds, ct);

        if (deleted > 0)
            logger.LogInformation(
                "Avatar cleanup: deleted {Deleted} unreferenced object(s) older than {CreatedBefore:u}, kept {Kept} referenced",
                deleted, createdBefore, keepFileIds.Count);
    }

    /// <summary>
    /// Extracts the avatar id from an avatar URL
    /// (<c>{Api:ApiUrl}/api/v1/avatar/{id}</c>).
    /// </summary>
    public static bool TryParseAvatarId(string? avatarUrl, out Guid avatarId)
    {
        avatarId = Guid.Empty;
        if (string.IsNullOrEmpty(avatarUrl))
            return false;

        var start = avatarUrl.LastIndexOf(AvatarPath, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return false;

        var tail = avatarUrl[(start + AvatarPath.Length)..];
        var end = tail.IndexOfAny(['?', '#', '/']);
        if (end >= 0)
            tail = tail[..end];

        return Guid.TryParse(tail, out avatarId);
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private TimeSpan GetTimeSpanSetting(string key, TimeSpan defaultValue, TimeSpan max)
    {
        var configured = configuration.GetValue<TimeSpan?>(key) ?? defaultValue;
        if (configured <= TimeSpan.Zero || configured > max)
        {
            logger.LogWarning("{Key}={Value} is out of range (0, {Max}], using {Default}",
                key, configured, max, defaultValue);
            return defaultValue;
        }

        return configured;
    }
}
