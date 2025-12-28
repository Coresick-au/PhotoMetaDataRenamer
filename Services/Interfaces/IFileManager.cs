using PhotoRenamer.Models;

namespace PhotoRenamer.Services.Interfaces;

/// <summary>
/// Interface for safe file operations
/// </summary>
public interface IFileManager
{
    /// <summary>
    /// Renames a file safely
    /// </summary>
    Task<OperationResult> RenameFileAsync(string oldPath, string newPath);
    
    /// <summary>
    /// Renames multiple files in a batch
    /// </summary>
    Task<List<OperationResult>> RenameFilesAsync(List<RenameOperation> operations, IProgress<int>? progress = null);
    
    /// <summary>
    /// Checks if an undo operation is available
    /// </summary>
    bool CanUndo();
    
    /// <summary>
    /// Undoes the last rename operation
    /// </summary>
    Task<bool> UndoLastOperationAsync();
    
    /// <summary>
    /// Gets the list of supported file extensions
    /// </summary>
    List<string> GetSupportedExtensions();
    
    /// <summary>
    /// Scans a directory for photo files
    /// </summary>
    Task<List<PhotoFile>> ScanDirectoryAsync(string path, bool recursive, IProgress<int>? progress = null);
}
