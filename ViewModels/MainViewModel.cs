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

    public MainViewModel(
        IMetadataReader metadataReader,
        ISuggestionEngine suggestionEngine,
        IFileManager fileManager,
        SettingsService settingsService)
    {
        _metadataReader = metadataReader;
        _suggestionEngine = suggestionEngine;
        _fileManager = fileManager;
        _settingsService = settingsService;

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

        IsProcessing = true;
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

            foreach (var file in files)
            {
                PhotoFiles.Add(file);
            }

            StatusMessage = $"Found {TotalFiles} photos. Click 'Generate Suggestions' to continue.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error scanning folder: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
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

        IsProcessing = true;
        ProcessingProgress = 0;
        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < PhotoFiles.Count; i++)
            {
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
        catch (Exception ex)
        {
            StatusMessage = $"Error generating suggestions: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRenames()
    {
        var selectedPhotos = PhotoFiles.Where(p => p.IsSelected && !string.IsNullOrEmpty(p.SelectedSuggestion)).ToList();
        
        if (selectedPhotos.Count == 0)
        {
            StatusMessage = "No photos selected for renaming.";
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to rename {selectedPhotos.Count} photos?",
            "Confirm Rename",
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

            var success = results.Count(r => r.Success);
            var failed = results.Count(r => !r.Success);

            StatusMessage = $"Renamed {success} files successfully. {failed} failed.";

            // Update file list with new names
            foreach (var (photo, opResult) in selectedPhotos.Zip(results))
            {
                if (opResult.Success)
                {
                    photo.FilePath = opResult.TargetPath;
                    photo.FileName = Path.GetFileName(opResult.TargetPath);
                }
            }

            OnPropertyChanged(nameof(CanUndo));
        }
        catch (Exception ex)
        {
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
