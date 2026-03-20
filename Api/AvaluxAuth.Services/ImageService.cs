using System.Reflection;
using AvaluxAuth.Abstractions;
using AvaluxAuth.Utils;
using Microsoft.Extensions.Configuration;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AvaluxAuth.Services;

public class ImageService(IConfiguration configuration) : IImageService
{
    private static readonly Color[] Colors =
    [
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
    ];

    public async Task<Stream> GenerateAvatar(string text = "", int? colorIndex = null, CancellationToken ct = default)
    {
        const int imageSize = 200;

        // Выбор цвета по индексу
        var bgColor = Colors[colorIndex ?? Random.Shared.Next(Colors.Length)].ToPixel<Rgba32>();

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

    public string CreateRandomAvatarUrl(string username)
    {
        return new UrlBuilder($"{configuration["Api.ApiUrl"]}/api/v1/avatar")
            .AddQuery("text", GenerateAvatarText(username))
            .AddQuery("color", Random.Shared.Next(Colors.Length).ToString())
            .ToString();
    }

    private static string GenerateAvatarText(string text)
    {
        var lst = new List<string>();

        foreach (var word in text.Split())
        {
            if (string.IsNullOrEmpty(word))
                continue;
            lst.Add(word[..1]);
            for (var i = 1; i < word.Length; i++)
            {
                var letter = word[i..(i + 1)];
                if (letter == letter.ToUpper())
                    lst.Add(letter);
            }
        }

        if (lst.Count == 0)
            return "";
        if (lst.Count == 1)
            return lst[0];
        return string.Join(string.Empty, lst.Slice(0, 2));
    }

    private const int MaxSize = 200;

    public async Task<Stream> ConvertToPng(Stream input, CancellationToken ct = default)
    {
        var image = await Image.LoadAsync(input, ct);

        // Вычисляем новый размер с сохранением пропорций
        int newWidth, newHeight;

        var newSize = int.Min(MaxSize, int.Min(image.Height, image.Width));
        if (image.Width < image.Height)
        {
            newWidth = newSize;
            newHeight = (int)((float)image.Height / image.Width * newSize);
        }
        else
        {
            newHeight = newSize;
            newWidth = (int)((float)image.Width / image.Height * newSize);
        }

        // Изменяем размер
        image.Mutate(x => x.Resize(newWidth, newHeight));

        // Создаем квадратное изображение с белым фоном
        using var squareImage = new Image<Rgba32>(newSize, newSize);

        squareImage.Mutate(ctx =>
        {
            // Заливаем фон (по умолчанию белый)
            ctx.BackgroundColor(Color.White);

            // Вычисляем позицию для центрирования
            var x = (newSize - newWidth) / 2;
            var y = (newSize - newHeight) / 2;

            // Вставляем измененное изображение по центру
            ctx.DrawImage(image, new Point(x, y), 1f);
        });

        var memoryStream = new MemoryStream();
        await squareImage.SaveAsPngAsync(memoryStream, ct);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }
}