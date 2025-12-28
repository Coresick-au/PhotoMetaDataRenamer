using PhotoRenamer.Models;

namespace PhotoRenamer.Services.Interfaces;

/// <summary>
/// Interface for generating filename suggestions based on metadata
/// </summary>
public interface ISuggestionEngine
{
    /// <summary>
    /// Generates filename suggestions for a photo based on its metadata
    /// </summary>
    Task<List<FilenameSuggestion>> GenerateSuggestionsAsync(PhotoMetadata metadata, NamingPattern pattern, string? customTag = null);
    
    /// <summary>
    /// Generates suggestions using all available patterns
    /// </summary>
    Task<List<FilenameSuggestion>> GenerateAllSuggestionsAsync(PhotoMetadata metadata);
    
    /// <summary>
    /// Gets all available naming patterns
    /// </summary>
    List<NamingPattern> GetAvailablePatterns();
    
    /// <summary>
    /// Sanitizes a filename to be Windows-compatible
    /// </summary>
    string SanitizeFilename(string filename);
    
    /// <summary>
    /// Resolves conflicts by appending sequence numbers
    /// </summary>
    string ResolveConflict(string baseName, string directory, string extension, HashSet<string> existingNames);
}
