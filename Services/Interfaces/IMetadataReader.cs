using PhotoRenamer.Models;

namespace PhotoRenamer.Services.Interfaces;

/// <summary>
/// Interface for extracting metadata from image files
/// </summary>
public interface IMetadataReader
{
    /// <summary>
    /// Extracts metadata from an image file
    /// </summary>
    PhotoMetadata ExtractMetadata(string filePath);
    
    /// <summary>
    /// Asynchronously extracts metadata from an image file
    /// </summary>
    Task<PhotoMetadata> ExtractMetadataAsync(string filePath);
    
    /// <summary>
    /// Checks if a file format is supported
    /// </summary>
    bool IsSupported(string filePath);
    
    /// <summary>
    /// Gets list of supported file extensions
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
