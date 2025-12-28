using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using PhotoRenamer.Models;
using PhotoRenamer.Services.Interfaces;

namespace PhotoRenamer.Services;

/// <summary>
/// Reverse geocoding service using OpenStreetMap Nominatim API
/// </summary>
public class GeocodingService : IGeocodingService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, LocationInfo> _cache = new();
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private const int MIN_REQUEST_INTERVAL_MS = 1000; // OSM requires 1 second between requests

    public int CacheCount => _cache.Count;

    public GeocodingService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PhotoRenamer/1.0 (Windows Desktop Application)");
    }

    public async Task<LocationInfo?> ReverseGeocodeAsync(double latitude, double longitude)
    {
        // Round coordinates to create cache key (reduces API calls for nearby locations)
        var cacheKey = $"{Math.Round(latitude, 4)},{Math.Round(longitude, 4)}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        await _rateLimiter.WaitAsync();
        try
        {
            // Ensure we respect rate limiting
            var elapsed = (DateTime.Now - _lastRequestTime).TotalMilliseconds;
            if (elapsed < MIN_REQUEST_INTERVAL_MS)
            {
                await Task.Delay((int)(MIN_REQUEST_INTERVAL_MS - elapsed));
            }

            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&zoom=16&addressdetails=1";
            
            var response = await _httpClient.GetAsync(url);
            _lastRequestTime = DateTime.Now;

            if (!response.IsSuccessStatusCode)
            {
                return CreateFallbackLocation(latitude, longitude);
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<NominatimResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (result?.Address == null)
            {
                return CreateFallbackLocation(latitude, longitude);
            }

            var locationInfo = new LocationInfo
            {
                // Prefer most specific to least specific location
                City = result.Address.Suburb ?? result.Address.Neighbourhood ?? result.Address.City ?? result.Address.Town ?? result.Address.Village ?? result.Address.Hamlet ?? result.Address.County,
                State = result.Address.State,
                Country = result.Address.Country,
                FormattedAddress = result.DisplayName
            };

            _cache[cacheKey] = locationInfo;
            return locationInfo;
        }
        catch (Exception)
        {
            return CreateFallbackLocation(latitude, longitude);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private LocationInfo CreateFallbackLocation(double latitude, double longitude)
    {
        return new LocationInfo
        {
            FormattedAddress = $"{latitude:F4}, {longitude:F4}"
        };
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimiter.Dispose();
    }

    // Response classes for JSON deserialization
    private class NominatimResponse
    {
        public string? DisplayName { get; set; }
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        public string? Suburb { get; set; }
        public string? Neighbourhood { get; set; }
        public string? City { get; set; }
        public string? Town { get; set; }
        public string? Village { get; set; }
        public string? Hamlet { get; set; }
        public string? State { get; set; }
        public string? County { get; set; }
        public string? Country { get; set; }
    }
}
