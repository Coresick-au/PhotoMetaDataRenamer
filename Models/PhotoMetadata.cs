namespace PhotoRenamer.Models;

/// <summary>
/// Represents metadata extracted from a photo file
/// </summary>
public class PhotoMetadata
{
    public DateTime? DateTaken { get; set; }
    public GpsCoordinates? Location { get; set; }
    public CameraInfo? Camera { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

/// <summary>
/// Represents GPS coordinates from photo EXIF data
/// </summary>
public class GpsCoordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }

    public override string ToString() => $"{Latitude:F6}, {Longitude:F6}";
}

/// <summary>
/// Represents camera information from photo EXIF data
/// </summary>
public class CameraInfo
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? LensModel { get; set; }
    public string? Settings { get; set; } // ISO, aperture, shutter speed
    
    public string DisplayName => string.IsNullOrEmpty(Make) ? Model : $"{Make} {Model}".Trim();
}

/// <summary>
/// Represents location information from reverse geocoding
/// </summary>
public class LocationInfo
{
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? State { get; set; }
    public string? FormattedAddress { get; set; }
    
    public string ShortName => City ?? State ?? Country ?? "Unknown";
}
