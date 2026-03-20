namespace AvaluxAuth.Abstractions;

public interface IImageService
{
    public Task<Stream> GenerateAvatar(string text = "", int? colorIndex = null, CancellationToken ct = default);
    public Task<Stream> ConvertToPng(Stream input, CancellationToken ct = default);
    public string CreateRandomAvatarUrl(string username);
}