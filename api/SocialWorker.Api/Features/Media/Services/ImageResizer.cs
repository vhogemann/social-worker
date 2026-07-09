using System;
using System.IO;
using SkiaSharp;

namespace SocialWorker.Api.Features.Media;

public sealed record ProcessImageResult(byte[] Data, int Width, int Height, string MimeType);

public sealed class ImageResizer
{
    public ProcessImageResult ProcessImage(Stream stream, string mimeType, int maxDimension = 1200)
    {
        using var codec = SKCodec.Create(stream) ?? throw new ArgumentException("Invalid image file format.");

        int originalWidth = codec.Info.Width;
        int originalHeight = codec.Info.Height;

        if (originalWidth > maxDimension || originalHeight > maxDimension)
        {
            double ratio = Math.Min((double)maxDimension / originalWidth, (double)maxDimension / originalHeight);
            int finalWidth = (int)(originalWidth * ratio);
            int finalHeight = (int)(originalHeight * ratio);

            using var original = SKBitmap.Decode(codec);
            using var resized = original.Resize(new SKImageInfo(finalWidth, finalHeight), SKFilterQuality.High);
            using var image = SKImage.FromBitmap(resized);

            var format = mimeType.Contains("png") ? SKEncodedImageFormat.Png :
                         mimeType.Contains("webp") ? SKEncodedImageFormat.Webp :
                         mimeType.Contains("gif") ? SKEncodedImageFormat.Gif :
                         SKEncodedImageFormat.Jpeg;

            using var data = image.Encode(format, 85);
            return new ProcessImageResult(data.ToArray(), finalWidth, finalHeight, mimeType);
        }
        else
        {
            using var ms = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(ms);
            return new ProcessImageResult(ms.ToArray(), originalWidth, originalHeight, mimeType);
        }
    }
}
