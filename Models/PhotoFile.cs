using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoRenamer.Models;

/// <summary>
/// Represents a photo file with its metadata and naming suggestions
/// </summary>
public partial class PhotoFile : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime FileCreated { get; set; }
    public DateTime FileModified { get; set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DateTakenDisplay))]
    [NotifyPropertyChangedFor(nameof(CameraDisplay))]
    [NotifyPropertyChangedFor(nameof(GpsDisplay))]
    private PhotoMetadata? _metadata;
    
    public List<FilenameSuggestion> Suggestions { get; set; } = new();
    
    [ObservableProperty]
    private string? _selectedSuggestion;
    
    [ObservableProperty]
    private bool _isSelected = true;
    
    [ObservableProperty]
    private byte[]? _thumbnail;
    
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
    
    // Display properties for UI binding
    public string? DateTakenDisplay => Metadata?.DateTaken?.ToString("yyyy-MM-dd HH:mm");
    public string? CameraDisplay => Metadata?.Camera?.DisplayName;
    public string? GpsDisplay => Metadata?.Location?.ToString();
    
    public string FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
    };
}

/// <summary>
/// Represents a filename suggestion for a photo
/// </summary>
public class FilenameSuggestion
{
    public string SuggestedName { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string PatternDescription { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
    public string? ConflictReason { get; set; }
}

/// <summary>
/// Represents a naming pattern template
/// </summary>
public class NamingPattern
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresLocation { get; set; }
    public bool RequiresCamera { get; set; }
}
