using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoRenamer.Services;

/// <summary>
/// Service for generating and caching image thumbnails
/// </summary>
public class ThumbnailService
{
    private const int ThumbnailSize = 100;

    /// <summary>
    /// Generates a thumbnail for an image file
    /// </summary>
    public byte[]? GenerateThumbnail(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return null;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);

            if (decoder.Frames.Count == 0)
                return null;

            var frame = decoder.Frames[0];
            
            // Calculate scaling to maintain aspect ratio
            double scale = Math.Min(
                ThumbnailSize / (double)frame.PixelWidth,
                ThumbnailSize / (double)frame.PixelHeight);

            var thumbnail = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));

            // Encode to PNG bytes
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(thumbnail));

            using var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);
            return memoryStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Asynchronously generates a thumbnail
    /// </summary>
    public Task<byte[]?> GenerateThumbnailAsync(string filePath)
    {
        return Task.Run(() => GenerateThumbnail(filePath));
    }
}
