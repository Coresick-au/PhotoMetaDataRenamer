using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PhotoRenamer.Models;
using PhotoRenamer.Services;
using PhotoRenamer.Services.Interfaces;

namespace PhotoRenamer.ViewModels;

/// <summary>
/// Main view model for the Photo Renamer application
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMetadataReader _metadataReader;
    private readonly ISuggestionEngine _suggestionEngine;
    private readonly IFileManager _fileManager;
    private readonly SettingsService _settingsService;
    private readonly ThumbnailService _thumbnailService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private ObservableCollection<PhotoFile> _photoFiles = new();

    [ObservableProperty]
    private NamingPattern? _selectedPattern;

    [ObservableProperty]
    private string? _selectedFolder;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _processingProgress;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private string _statusMessage = "Select a folder to get started";

    [ObservableProperty]
    private bool _recursiveScan = true;

    [ObservableProperty]
    private string? _customTag;

    [ObservableProperty]
    private PhotoFile? _selectedPhoto;

    public ObservableCollection<NamingPattern> AvailablePatterns { get; } = new();

    public bool CanApplyRename => PhotoFiles.Any(p => p.IsSelected && !string.IsNullOrEmpty(p.SelectedSuggestion));
    public bool CanUndo => _fileManager.CanUndo();
    public bool CanCancel => IsProcessing && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;

    public MainViewModel(
        IMetadataReader metadataReader,
        ISuggestionEngine suggestionEngine,
        IFileManager fileManager,
        SettingsService settingsService,
        ThumbnailService thumbnailService)
    {
        _metadataReader = metadataReader;
        _suggestionEngine = suggestionEngine;
        _fileManager = fileManager;
        _settingsService = settingsService;
        _thumbnailService = thumbnailService;

        // Load available patterns
        foreach (var pattern in _suggestionEngine.GetAvailablePatterns())
        {
            AvailablePatterns.Add(pattern);
        }
        
        // Load saved preferences
        var settings = _settingsService.Settings;
        RecursiveScan = settings.RecursiveScan;
        SelectedFolder = settings.LastFolderPath;
        
        // Restore last selected pattern
        var lastPattern = AvailablePatterns.FirstOrDefault(p => p.Id == settings.LastPatternId);
        SelectedPattern = lastPattern ?? AvailablePatterns.FirstOrDefault();
    }

    // Called when SelectedPattern changes
    partial void OnSelectedPatternChanged(NamingPattern? value)
    {
        if (value != null)
        {
            _settingsService.UpdateSelectedPattern(value.Id);
        }
    }

    // Called when RecursiveScan changes
    partial void OnRecursiveScanChanged(bool value)
    {
        _settingsService.UpdateRecursiveScan(value);
    }

    [RelayCommand]
    private async Task SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Photo Folder",
            InitialDirectory = SelectedFolder
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.FolderName;
            _settingsService.UpdateLastFolder(dialog.FolderName);
            await ScanFolderAsync();
        }
    }

    private async Task ScanFolderAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolder))
            return;

        // Cancel any previous operation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        IsProcessing = true;
        OnPropertyChanged(nameof(CanCancel));
        StatusMessage = "Scanning folder...";
        PhotoFiles.Clear();
        ProcessingProgress = 0;

        try
        {
            var progress = new Progress<int>(count =>
            {
                ProcessingProgress = count;
                StatusMessage = $"Found {count} photos...";
            });

            var files = await _fileManager.ScanDirectoryAsync(SelectedFolder, RecursiveScan, progress);
            TotalFiles = files.Count;

            // Generate thumbnails in background
            StatusMessage = $"Found {TotalFiles} photos. Generating thumbnails...";
            int thumbnailCount = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                file.Thumbnail = await _thumbnailService.GenerateThumbnailAsync(file.FilePath);
                thumbnailCount++;
                if (thumbnailCount % 5 == 0)
                {
                    StatusMessage = $"Generated thumbnails: {thumbnailCount}/{TotalFiles}";
                    ProcessingProgress = (int)((thumbnailCount * 100.0) / TotalFiles);
                }
                PhotoFiles.Add(file);
            }

            StatusMessage = $"Found {TotalFiles} photos. Click 'Generate Suggestions' to continue.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Operation cancelled. {PhotoFiles.Count} photos loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning folder: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Cancelling operation...";
        OnPropertyChanged(nameof(CanCancel));
    }

    [RelayCommand]
    private async Task GenerateSuggestions()
    {
        if (PhotoFiles.Count == 0)
        {
            StatusMessage = "No photos to process. Select a folder first.";
            return;
        }

        if (SelectedPattern == null)
        {
            StatusMessage = "Please select a naming pattern first.";
            return;
        }

        // Cancel any previous operation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        IsProcessing = true;
        OnPropertyChanged(nameof(CanCancel));
        ProcessingProgress = 0;
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < PhotoFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var photo = PhotoFiles[i];
                StatusMessage = $"Processing {i + 1} of {PhotoFiles.Count}: {photo.FileName}";
                ProcessingProgress = (int)((i + 1.0) / PhotoFiles.Count * 100);

                // Always extract metadata fresh (in case user re-runs with changes)
                try
                {
                    photo.Metadata = await _metadataReader.ExtractMetadataAsync(photo.FilePath);
                }
                catch (Exception ex)
                {
                    // Create minimal metadata with error info
                    photo.Metadata = new PhotoMetadata
                    {
                        OriginalFileName = photo.FileName
                    };
                    photo.Metadata.AdditionalProperties["ExtractionError"] = ex.Message;
                    System.Diagnostics.Debug.WriteLine($"Metadata extraction failed for {photo.FileName}: {ex.Message}");
                }

                // Generate suggestion using the SELECTED pattern
                var suggestions = await _suggestionEngine.GenerateSuggestionsAsync(photo.Metadata, SelectedPattern, CustomTag);
                
                // Also get all patterns for reference
                photo.Suggestions = await _suggestionEngine.GenerateAllSuggestionsAsync(photo.Metadata);

                // Use the selected pattern's suggestion, or fallback to first available
                string baseName;
                if (suggestions.Count > 0)
                {
                    baseName = suggestions[0].SuggestedName;
                }
                else if (photo.Suggestions.Count > 0)
                {
                    // Fallback to first available pattern if selected pattern can't generate
                    baseName = photo.Suggestions[0].SuggestedName;
                    StatusMessage = $"Pattern '{SelectedPattern.Name}' unavailable for {photo.FileName}, using fallback.";
                }
                else
                {
                    // Ultimate fallback - use original filename without extension
                    baseName = Path.GetFileNameWithoutExtension(photo.FileName);
                }

                // Resolve conflicts
                var resolved = _suggestionEngine.ResolveConflict(
                    baseName, 
                    photo.DirectoryPath, 
                    photo.FileExtension, 
                    existingNames);
                
                photo.SelectedSuggestion = resolved + photo.FileExtension;
                existingNames.Add(photo.SelectedSuggestion);
            }

            StatusMessage = $"Generated suggestions using '{SelectedPattern.Name}' pattern for {PhotoFiles.Count} photos. Review and click 'Apply Renames'.";
            OnPropertyChanged(nameof(CanApplyRename));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"Generation cancelled. {PhotoFiles.Count(p => !string.IsNullOrEmpty(p.SelectedSuggestion))} suggestions generated.";
            OnPropertyChanged(nameof(CanApplyRename));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating suggestions: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    [RelayCommand]
    private async Task ApplyRenames()
    {
        var selectedPhotos = PhotoFiles.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.SelectedSuggestion)).ToList();
        
        if (selectedPhotos.Count == 0)
        {
            MessageBox.Show(
                "No photos selected for renaming.\n\nPlease select photos using the checkboxes and ensure they have suggested names.",
                "No Photos Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Build detailed confirmation message
        var confirmMessage = $"Ready to rename {selectedPhotos.Count} photo(s).\n\n" +
                            $"Preview of changes:\n";
        
        var previewCount = Math.Min(5, selectedPhotos.Count);
        for (int i = 0; i < previewCount; i++)
        {
            var p = selectedPhotos[i];
            confirmMessage += $"  • {p.FileName} → {p.SelectedSuggestion}\n";
        }
        
        if (selectedPhotos.Count > 5)
        {
            confirmMessage += $"  ... and {selectedPhotos.Count - 5} more\n";
        }
        
        confirmMessage += "\nDo you want to proceed?";

        var result = MessageBox.Show(
            confirmMessage,
            "Confirm Rename Operation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsProcessing = true;
        ProcessingProgress = 0;

        try
        {
            var operations = selectedPhotos.Select(p => new RenameOperation
            {
                SourcePath = p.FilePath,
                TargetPath = Path.Combine(p.DirectoryPath, p.SelectedSuggestion!),
                Metadata = p.Metadata
            }).ToList();

            var progress = new Progress<int>(count =>
            {
                ProcessingProgress = (int)((count * 1.0) / operations.Count * 100);
                StatusMessage = $"Renaming {count} of {operations.Count}...";
            });

            var results = await _fileManager.RenameFilesAsync(operations, progress);

            var successResults = results.Where(r => r.Success).ToList();
            var failedResults = results.Where(r => !r.Success).ToList();

            // Update file list with new names for successful operations
            foreach (var (photo, opResult) in selectedPhotos.Zip(results))
            {
                if (opResult.Success)
                {
                    photo.FilePath = opResult.TargetPath;
                    photo.FileName = Path.GetFileName(opResult.TargetPath);
                }
            }

            // Show detailed result summary
            var resultMessage = $"Operation Complete!\n\n" +
                               $"✓ Successfully renamed: {successResults.Count}\n";
            
            if (failedResults.Count > 0)
            {
                resultMessage += $"✗ Failed: {failedResults.Count}\n\n" +
                                "Failed operations:\n";
                foreach (var fail in failedResults.Take(5))
                {
                    resultMessage += $"  • {Path.GetFileName(fail.SourcePath)}: {fail.ErrorMessage}\n";
                }
                if (failedResults.Count > 5)
                {
                    resultMessage += $"  ... and {failedResults.Count - 5} more errors\n";
                }
            }

            MessageBox.Show(
                resultMessage,
                failedResults.Count > 0 ? "Rename Complete (with errors)" : "Rename Complete",
                MessageBoxButton.OK,
                failedResults.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            StatusMessage = $"Renamed {successResults.Count} files. {failedResults.Count} failed.";
            OnPropertyChanged(nameof(CanUndo));
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An error occurred during the rename operation:\n\n{ex.Message}\n\nSome files may have been renamed. Use Undo to revert changes.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusMessage = $"Error renaming files: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task Undo()
    {
        if (!_fileManager.CanUndo())
        {
            StatusMessage = "Nothing to undo.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Undoing last operation...";

        try
        {
            var success = await _fileManager.UndoLastOperationAsync();
            StatusMessage = success ? "Undo successful." : "Undo partially failed.";
            
            // Refresh the folder
            if (!string.IsNullOrEmpty(SelectedFolder))
            {
                await ScanFolderAsync();
            }

            OnPropertyChanged(nameof(CanUndo));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during undo: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var photo in PhotoFiles)
        {
            photo.IsSelected = true;
        }
        OnPropertyChanged(nameof(CanApplyRename));
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var photo in PhotoFiles)
        {
            photo.IsSelected = false;
        }
        OnPropertyChanged(nameof(CanApplyRename));
    }

    [RelayCommand]
    private void RefreshFolder()
    {
        if (!string.IsNullOrEmpty(SelectedFolder))
        {
            _ = ScanFolderAsync();
        }
    }
}
