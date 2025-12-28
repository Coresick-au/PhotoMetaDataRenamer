using System.IO;
using System.Text.RegularExpressions;
using PhotoRenamer.Models;
using PhotoRenamer.Services.Interfaces;

namespace PhotoRenamer.Services;

/// <summary>
/// Generates intelligent filename suggestions based on photo metadata
/// </summary>
public partial class SuggestionEngine : ISuggestionEngine
{
    private readonly IGeocodingService _geocodingService;
    
    // Invalid Windows filename characters
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
    private const int MAX_FILENAME_LENGTH = 200; // Leave room for path

    private readonly List<NamingPattern> _availablePatterns = new()
    {
        new NamingPattern
        {
            Id = "date_time",
            Name = "Date & Time",
            Template = "{date}_{time}",
            Description = "YYYY-MM-DD_HH-mm",
            RequiresLocation = false,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "date_only",
            Name = "Date Only",
            Template = "{date}",
            Description = "YYYY-MM-DD",
            RequiresLocation = false,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "date_location",
            Name = "Date & Location",
            Template = "{date}_{location}",
            Description = "YYYY-MM-DD_CityName",
            RequiresLocation = true,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "date_location_time",
            Name = "Date, Location & Time",
            Template = "{date}_{location}_{time}",
            Description = "YYYY-MM-DD_Suburb_HH-mm",
            RequiresLocation = true,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "date_camera",
            Name = "Date & Camera",
            Template = "{date}_{camera}",
            Description = "YYYY-MM-DD_CameraModel",
            RequiresLocation = false,
            RequiresCamera = true
        },
        new NamingPattern
        {
            Id = "date_custom",
            Name = "Date & Custom",
            Template = "{date}_{custom}",
            Description = "YYYY-MM-DD_YourText",
            RequiresLocation = false,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "date_location_custom",
            Name = "Date, Location & Custom",
            Template = "{date}_{location}_{custom}",
            Description = "YYYY-MM-DD_Suburb_YourText",
            RequiresLocation = true,
            RequiresCamera = false
        },
        new NamingPattern
        {
            Id = "full",
            Name = "Full Details",
            Template = "{date}_{time}_{location}_{camera}",
            Description = "YYYY-MM-DD_HH-mm_Suburb_Camera",
            RequiresLocation = true,
            RequiresCamera = true
        }
    };

    public SuggestionEngine(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    public List<NamingPattern> GetAvailablePatterns() => _availablePatterns;

    public async Task<List<FilenameSuggestion>> GenerateAllSuggestionsAsync(PhotoMetadata metadata)
    {
        var suggestions = new List<FilenameSuggestion>();
        
        foreach (var pattern in _availablePatterns)
        {
            // Skip patterns that require data we don't have
            if (pattern.RequiresLocation && metadata.Location == null)
                continue;
            if (pattern.RequiresCamera && metadata.Camera == null)
                continue;

            var patternSuggestions = await GenerateSuggestionsAsync(metadata, pattern);
            suggestions.AddRange(patternSuggestions);
        }

        return suggestions;
    }

    public async Task<List<FilenameSuggestion>> GenerateSuggestionsAsync(PhotoMetadata metadata, NamingPattern pattern, string? customTag = null)
    {
        var suggestions = new List<FilenameSuggestion>();

        var filename = await BuildFilenameFromPattern(metadata, pattern, customTag);
        
        if (!string.IsNullOrEmpty(filename))
        {
            var sanitized = SanitizeFilename(filename);
            suggestions.Add(new FilenameSuggestion
            {
                SuggestedName = sanitized,
                Pattern = pattern.Id,
                PatternDescription = pattern.Description
            });
        }

        return suggestions;
    }

    private async Task<string> BuildFilenameFromPattern(PhotoMetadata metadata, NamingPattern pattern, string? customTag = null)
    {
        var result = pattern.Template;

        // Replace date placeholder
        if (result.Contains("{date}"))
        {
            var dateStr = metadata.DateTaken?.ToString("yyyy-MM-dd") ?? "Unknown";
            result = result.Replace("{date}", dateStr);
        }

        // Replace time placeholder
        if (result.Contains("{time}"))
        {
            var timeStr = metadata.DateTaken?.ToString("HH-mm") ?? "00-00";
            result = result.Replace("{time}", timeStr);
        }

        // Replace location placeholder
        if (result.Contains("{location}"))
        {
            var location = "Unknown";
            if (metadata.Location != null)
            {
                var locationInfo = await _geocodingService.ReverseGeocodeAsync(
                    metadata.Location.Latitude, 
                    metadata.Location.Longitude);
                
                if (locationInfo != null)
                {
                    location = locationInfo.ShortName;
                }
            }
            result = result.Replace("{location}", location);
        }

        // Replace camera placeholder
        if (result.Contains("{camera}"))
        {
            var camera = metadata.Camera?.Model ?? "UnknownCamera";
            // Simplify camera model name
            camera = SimplifyCameraName(camera);
            result = result.Replace("{camera}", camera);
        }

        // Replace custom placeholder
        if (result.Contains("{custom}"))
        {
            var custom = !string.IsNullOrWhiteSpace(customTag) ? customTag.Trim() : "Custom";
            result = result.Replace("{custom}", custom);
        }

        return result;
    }

    private string SimplifyCameraName(string model)
    {
        // Remove common redundant words and clean up
        var simplified = model
            .Replace("Digital Camera", "")
            .Replace("DIGITAL CAMERA", "")
            .Trim();

        // Remove spaces and special chars for filename
        simplified = Regex.Replace(simplified, @"[^a-zA-Z0-9]", "");
        
        // Limit length
        if (simplified.Length > 20)
            simplified = simplified[..20];

        return string.IsNullOrEmpty(simplified) ? "Camera" : simplified;
    }

    public string SanitizeFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "unnamed";

        // Remove invalid characters
        foreach (var c in InvalidChars)
        {
            filename = filename.Replace(c, '_');
        }

        // Replace multiple underscores/spaces with single underscore
        filename = Regex.Replace(filename, @"[_\s]+", "_");
        
        // Remove leading/trailing underscores and dots
        filename = filename.Trim('_', '.', ' ');

        // Truncate if too long
        if (filename.Length > MAX_FILENAME_LENGTH)
        {
            filename = filename[..MAX_FILENAME_LENGTH];
        }

        // Ensure we have a valid filename
        if (string.IsNullOrEmpty(filename))
            return "unnamed";

        return filename;
    }

    public string ResolveConflict(string baseName, string directory, string extension, HashSet<string> existingNames)
    {
        var fullName = $"{baseName}{extension}";
        
        if (!existingNames.Contains(fullName) && !File.Exists(Path.Combine(directory, fullName)))
        {
            return baseName;
        }

        // Try adding sequence numbers
        for (int i = 1; i <= 9999; i++)
        {
            var newName = $"{baseName}_{i:D4}";
            fullName = $"{newName}{extension}";
            
            if (!existingNames.Contains(fullName) && !File.Exists(Path.Combine(directory, fullName)))
            {
                return newName;
            }
        }

        // Fallback to GUID if somehow we have 9999 conflicts
        return $"{baseName}_{Guid.NewGuid():N}";
    }
}
