namespace AvaluxAuth.Abstractions;

public interface IImageService
{
    public Task<Stream> GenerateAvatar(string text = "", int? colorIndex = null, CancellationToken ct = default);
    public Task<Stream> ConvertToPng(Stream input, CancellationToken ct = default);

    /// <summary>
    /// Normalises an uploaded application icon: fits it into a square canvas
    /// while keeping the aspect ratio and, unlike <see cref="ConvertToPng"/>,
    /// keeps transparency (icons are shown on both light and dark themes).
    /// </summary>
    public Task<Stream> ConvertIconToPng(Stream input, CancellationToken ct = default);

    public string CreateRandomAvatarUrl(string username);
}