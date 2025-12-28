using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoRenamer.Models;
using PhotoRenamer.Services.Interfaces;

namespace PhotoRenamer.Services;

/// <summary>
/// Extracts metadata from image files using MetadataExtractor.NET
/// </summary>
public class MetadataReader : IMetadataReader
{
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".gif", ".bmp",
        ".webp", ".heic", ".heif",
        // RAW formats
        ".raw", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".pef", ".srw"
    };

    public IReadOnlyList<string> SupportedExtensions => _supportedExtensions.ToList();

    public bool IsSupported(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _supportedExtensions.Contains(extension);
    }

    public PhotoMetadata ExtractMetadata(string filePath)
    {
        var metadata = new PhotoMetadata
        {
            OriginalFileName = Path.GetFileName(filePath)
        };

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            // Extract date/time
            metadata.DateTaken = ExtractDateTaken(directories);

            // Extract GPS coordinates
            metadata.Location = ExtractGpsCoordinates(directories);

            // Extract camera information
            metadata.Camera = ExtractCameraInfo(directories);

            // If no EXIF date, fall back to file dates
            if (metadata.DateTaken == null)
            {
                var fileInfo = new FileInfo(filePath);
                metadata.DateTaken = fileInfo.CreationTime < fileInfo.LastWriteTime 
                    ? fileInfo.CreationTime 
                    : fileInfo.LastWriteTime;
            }
        }
        catch (Exception ex)
        {
            // If metadata extraction fails, fall back to file system dates
            var fileInfo = new FileInfo(filePath);
            metadata.DateTaken = fileInfo.CreationTime < fileInfo.LastWriteTime 
                ? fileInfo.CreationTime 
                : fileInfo.LastWriteTime;
            
            metadata.AdditionalProperties["ExtractionError"] = ex.Message;
        }

        return metadata;
    }

    public async Task<PhotoMetadata> ExtractMetadataAsync(string filePath)
    {
        return await Task.Run(() => ExtractMetadata(filePath));
    }

    private DateTime? ExtractDateTaken(IEnumerable<MetadataExtractor.Directory> directories)
    {
        // Try EXIF SubIFD first (DateTimeOriginal)
        var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (exifSubIfd != null)
        {
            if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateOriginal))
                return dateOriginal;
            
            if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var dateDigitized))
                return dateDigitized;
        }

        // Try EXIF IFD0 (DateTime)
        var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (exifIfd0 != null)
        {
            if (exifIfd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime))
                return dateTime;
        }

        return null;
    }

    private GpsCoordinates? ExtractGpsCoordinates(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var gpsDir = directories.OfType<GpsDirectory>().FirstOrDefault();
        if (gpsDir == null)
            return null;

        var geoLocation = gpsDir.GetGeoLocation();
        if (geoLocation == null)
            return null;

        var coordinates = new GpsCoordinates
        {
            Latitude = geoLocation.Latitude,
            Longitude = geoLocation.Longitude
        };

        // Try to get altitude
        if (gpsDir.TryGetRational(GpsDirectory.TagAltitude, out var altitude))
        {
            var altitudeValue = altitude.ToDouble();
            
            // Check altitude reference (0 = above sea level, 1 = below)
            if (gpsDir.TryGetByte(GpsDirectory.TagAltitudeRef, out var altRef) && altRef == 1)
            {
                altitudeValue = -altitudeValue;
            }
            
            coordinates.Altitude = altitudeValue;
        }

        return coordinates;
    }

    private CameraInfo? ExtractCameraInfo(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (exifIfd0 == null)
            return null;

        var make = exifIfd0.GetString(ExifDirectoryBase.TagMake)?.Trim();
        var model = exifIfd0.GetString(ExifDirectoryBase.TagModel)?.Trim();

        if (string.IsNullOrEmpty(make) && string.IsNullOrEmpty(model))
            return null;

        var camera = new CameraInfo
        {
            Make = make ?? string.Empty,
            Model = model ?? string.Empty
        };

        // Try to get lens model
        var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (exifSubIfd != null)
        {
            camera.LensModel = exifSubIfd.GetString(ExifDirectoryBase.TagLensModel)?.Trim();

            // Build settings string
            var settings = new List<string>();

            if (exifSubIfd.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso))
                settings.Add($"ISO {iso}");

            if (exifSubIfd.TryGetRational(ExifDirectoryBase.TagFNumber, out var fNumber))
                settings.Add($"f/{fNumber.ToDouble():F1}");

            if (exifSubIfd.TryGetRational(ExifDirectoryBase.TagExposureTime, out var exposure))
            {
                var exposureValue = exposure.ToDouble();
                if (exposureValue >= 1)
                    settings.Add($"{exposureValue:F1}s");
                else
                    settings.Add($"1/{(int)(1 / exposureValue)}s");
            }

            if (exifSubIfd.TryGetRational(ExifDirectoryBase.TagFocalLength, out var focal))
                settings.Add($"{focal.ToDouble():F0}mm");

            if (settings.Count > 0)
                camera.Settings = string.Join(", ", settings);
        }

        return camera;
    }
}
