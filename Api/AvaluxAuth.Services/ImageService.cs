using System.Reflection;
using AvaluxAuth.Abstractions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AvaluxAuth.Services;

public class ImageService : IImageService
{
    public async Task<Stream> GenerateAvatar(string text = "", int? colorIndex = null, CancellationToken ct = default)
    {
        const int imageSize = 200;
        var colors = new[]
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.Yellow,
            Color.Orange,
            Color.Pink,
            Color.Purple,
            Color.Aqua,
            Color.Violet,
            Color.Gray,
        };

        // Выбор цвета по индексу
        var bgColor = colors[colorIndex ?? Random.Shared.Next(colors.Length)].ToPixel<Rgba32>();

        using var image = new Image<Rgba32>(imageSize, imageSize);

        image.Mutate(ctx =>
        {
            ctx.BackgroundColor(bgColor);

            var font = GetFont();

            var textOptions = new RichTextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Origin = new PointF(imageSize / 2f, imageSize / 2f)
            };

            ctx.DrawText(textOptions, text, (bgColor.R + bgColor.G + bgColor.B) > 400 ? Color.Black : Color.White);
        });

        // Сохраняем в PNG
        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream, ct);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    private static Font GetFont()
    {
        const float fontSize = 100;

        var assembly = Assembly.GetExecutingAssembly();
        Console.WriteLine(string.Join(';', assembly.GetManifestResourceNames()));
        var resourceStream = assembly.GetManifestResourceStream("AvaluxAuth.Services.Resources.OpenSans-Regular.ttf");

        if (resourceStream == null)
            throw new Exception("Font not found");

        var fontCollection = new FontCollection();
        var fontFamily = fontCollection.Add(resourceStream);
        return new Font(fontFamily, fontSize, FontStyle.Regular);
    }
}