using PhotoRenamer.Models;

namespace PhotoRenamer.Services.Interfaces;

/// <summary>
/// Interface for reverse geocoding GPS coordinates
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Converts GPS coordinates to a location name
    /// </summary>
    Task<LocationInfo?> ReverseGeocodeAsync(double latitude, double longitude);
    
    /// <summary>
    /// Clears the geocoding cache
    /// </summary>
    void ClearCache();
    
    /// <summary>
    /// Gets the number of cached locations
    /// </summary>
    int CacheCount { get; }
}
