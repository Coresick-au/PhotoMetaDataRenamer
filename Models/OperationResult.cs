namespace PhotoRenamer.Models;

/// <summary>
/// Represents the result of a file operation
/// </summary>
public class OperationResult
{
    public bool Success { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    
    public static OperationResult Succeeded(string sourcePath, string targetPath) => new()
    {
        Success = true,
        SourcePath = sourcePath,
        TargetPath = targetPath
    };
    
    public static OperationResult Failed(string sourcePath, string targetPath, string message, Exception? ex = null) => new()
    {
        Success = false,
        SourcePath = sourcePath,
        TargetPath = targetPath,
        ErrorMessage = message,
        Exception = ex
    };
}

/// <summary>
/// Represents a rename operation to be executed
/// </summary>
public class RenameOperation
{
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public PhotoMetadata? Metadata { get; set; }
}

/// <summary>
/// Records a completed operation for undo functionality
/// </summary>
public class OperationRecord
{
    public DateTime Timestamp { get; set; }
    public List<RenameOperation> Operations { get; set; } = new();
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}
