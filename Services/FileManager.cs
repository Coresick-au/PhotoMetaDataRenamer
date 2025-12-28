using System.IO;
using PhotoRenamer.Models;
using PhotoRenamer.Services.Interfaces;

namespace PhotoRenamer.Services;

/// <summary>
/// Manages file operations including renaming, scanning, and undo functionality
/// </summary>
public class FileManager : IFileManager
{
    private readonly IMetadataReader _metadataReader;
    private readonly Stack<OperationRecord> _operationHistory = new();
    
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".gif", ".bmp",
        ".webp", ".heic", ".heif",
        ".raw", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".pef", ".srw"
    };

    public FileManager(IMetadataReader metadataReader)
    {
        _metadataReader = metadataReader;
    }

    public List<string> GetSupportedExtensions() => _supportedExtensions.ToList();

    public async Task<List<PhotoFile>> ScanDirectoryAsync(string path, bool recursive, IProgress<int>? progress = null)
    {
        var result = new List<PhotoFile>();
        
        if (!Directory.Exists(path))
            return result;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(path, "*.*", searchOption)
            .Where(f => _supportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var count = 0;
        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var photoFile = new PhotoFile
                {
                    FilePath = filePath,
                    FileName = fileInfo.Name,
                    FileExtension = fileInfo.Extension,
                    FileSize = fileInfo.Length,
                    FileCreated = fileInfo.CreationTime,
                    FileModified = fileInfo.LastWriteTime
                };

                result.Add(photoFile);
                count++;
                progress?.Report(count);
            }
            catch (Exception)
            {
                // Skip files we can't access
            }
        }

        return result;
    }

    public async Task<OperationResult> RenameFileAsync(string oldPath, string newPath)
    {
        try
        {
            if (!File.Exists(oldPath))
            {
                return OperationResult.Failed(oldPath, newPath, "Source file does not exist.");
            }

            if (File.Exists(newPath))
            {
                return OperationResult.Failed(oldPath, newPath, "Target file already exists.");
            }

            // Preserve file attributes and timestamps
            var fileInfo = new FileInfo(oldPath);
            var creationTime = fileInfo.CreationTime;
            var lastWriteTime = fileInfo.LastWriteTime;
            var attributes = fileInfo.Attributes;

            await Task.Run(() => File.Move(oldPath, newPath));

            // Restore timestamps
            var newFileInfo = new FileInfo(newPath);
            newFileInfo.CreationTime = creationTime;
            newFileInfo.LastWriteTime = lastWriteTime;
            newFileInfo.Attributes = attributes;

            return OperationResult.Succeeded(oldPath, newPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Failed(oldPath, newPath, "Access denied. Check file permissions.", ex);
        }
        catch (IOException ex)
        {
            return OperationResult.Failed(oldPath, newPath, $"File operation failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return OperationResult.Failed(oldPath, newPath, ex.Message, ex);
        }
    }

    public async Task<List<OperationResult>> RenameFilesAsync(List<RenameOperation> operations, IProgress<int>? progress = null)
    {
        var results = new List<OperationResult>();
        var successfulOperations = new List<RenameOperation>();
        var count = 0;

        foreach (var operation in operations)
        {
            var result = await RenameFileAsync(operation.SourcePath, operation.TargetPath);
            results.Add(result);

            if (result.Success)
            {
                successfulOperations.Add(new RenameOperation
                {
                    SourcePath = operation.TargetPath, // Swap for undo
                    TargetPath = operation.SourcePath
                });
            }

            count++;
            progress?.Report(count);
        }

        // Record operation for undo
        if (successfulOperations.Count > 0)
        {
            _operationHistory.Push(new OperationRecord
            {
                Timestamp = DateTime.Now,
                Operations = successfulOperations,
                SuccessCount = results.Count(r => r.Success),
                FailureCount = results.Count(r => !r.Success)
            });
        }

        return results;
    }

    public bool CanUndo() => _operationHistory.Count > 0;

    public async Task<bool> UndoLastOperationAsync()
    {
        if (!CanUndo())
            return false;

        var record = _operationHistory.Pop();
        var allSuccess = true;

        foreach (var operation in record.Operations)
        {
            var result = await RenameFileAsync(operation.SourcePath, operation.TargetPath);
            if (!result.Success)
                allSuccess = false;
        }

        return allSuccess;
    }
}
