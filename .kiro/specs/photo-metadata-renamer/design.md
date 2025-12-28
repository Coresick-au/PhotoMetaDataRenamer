# Design Document: Photo Metadata Renamer

## Overview

The Photo Metadata Renamer is a Windows desktop application built using WPF (.NET) that extracts metadata from photo files and provides intelligent renaming suggestions. The application follows a clean architecture pattern with separate layers for UI, business logic, and data access, ensuring maintainability and testability.

## Architecture

The application uses a layered architecture with the following components:

```
┌─────────────────────────────────────┐
│           WPF UI Layer              │
│  (MainWindow, ViewModels, Views)    │
├─────────────────────────────────────┤
│         Business Logic Layer       │
│  (Services, Suggestion Engine)     │
├─────────────────────────────────────┤
│          Data Access Layer         │
│  (Metadata Reader, File Manager)   │
├─────────────────────────────────────┤
│         External Libraries         │
│  (MetadataExtractor, Geocoding)    │
└─────────────────────────────────────┘
```

The application will be implemented using:
- **Framework**: WPF with .NET 8
- **Architecture Pattern**: MVVM (Model-View-ViewModel)
- **Metadata Library**: MetadataExtractor.NET for EXIF data extraction
- **Geocoding**: OpenStreetMap Nominatim API for reverse geocoding
- **UI Framework**: WPF with Material Design themes

## Components and Interfaces

### 1. Metadata Reader Component

**Purpose**: Extract metadata from image files using MetadataExtractor.NET library.

**Interface**:
```csharp
public interface IMetadataReader
{
    PhotoMetadata ExtractMetadata(string filePath);
    Task<PhotoMetadata> ExtractMetadataAsync(string filePath);
    bool IsSupported(string filePath);
}

public class PhotoMetadata
{
    public DateTime? DateTaken { get; set; }
    public GpsCoordinates? Location { get; set; }
    public CameraInfo? Camera { get; set; }
    public string OriginalFileName { get; set; }
    public Dictionary<string, object> AdditionalProperties { get; set; }
}
```

**Responsibilities**:
- Extract EXIF date/time information
- Extract GPS coordinates when available
- Extract camera make, model, and settings
- Handle various image formats (JPEG, TIFF, PNG, RAW)
- Provide fallback to file system dates when EXIF is unavailable

### 2. Suggestion Engine Component

**Purpose**: Generate intelligent filename suggestions based on extracted metadata.

**Interface**:
```csharp
public interface ISuggestionEngine
{
    Task<List<FilenameSuggestion>> GenerateSuggestionsAsync(PhotoMetadata metadata, NamingPattern pattern);
    List<NamingPattern> GetAvailablePatterns();
    string SanitizeFilename(string filename);
}

public class FilenameSuggestion
{
    public string SuggestedName { get; set; }
    public string Pattern { get; set; }
    public bool HasConflict { get; set; }
    public string ConflictReason { get; set; }
}
```

**Naming Patterns**:
- `YYYY-MM-DD_HHmmss` (date and time)
- `YYYY-MM-DD_Location` (date and location)
- `YYYY-MM-DD_Camera_Model` (date and camera)
- `Location_YYYY-MM-DD` (location first)
- Custom user-defined patterns

**Responsibilities**:
- Generate multiple naming suggestions per photo
- Handle location reverse geocoding via OpenStreetMap
- Resolve filename conflicts with sequence numbers
- Sanitize filenames for Windows compatibility
- Cache geocoding results to minimize API calls

### 3. File Manager Component

**Purpose**: Handle safe file operations and maintain operation history.

**Interface**:
```csharp
public interface IFileManager
{
    Task<OperationResult> RenameFileAsync(string oldPath, string newPath);
    Task<List<OperationResult>> RenameFilesAsync(List<RenameOperation> operations);
    bool CanUndo();
    Task<bool> UndoLastOperationAsync();
    List<string> GetSupportedExtensions();
}

public class RenameOperation
{
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public PhotoMetadata Metadata { get; set; }
}
```

**Responsibilities**:
- Perform atomic file rename operations
- Maintain operation history for undo functionality
- Handle file conflicts and permission errors
- Preserve file attributes and timestamps
- Validate target paths and filenames

### 4. Geocoding Service

**Purpose**: Convert GPS coordinates to human-readable location names.

**Interface**:
```csharp
public interface IGeocodingService
{
    Task<LocationInfo> ReverseGeocodeAsync(double latitude, double longitude);
    void ClearCache();
}

public class LocationInfo
{
    public string City { get; set; }
    public string Country { get; set; }
    public string State { get; set; }
    public string FormattedAddress { get; set; }
}
```

**Implementation Details**:
- Uses OpenStreetMap Nominatim API (free, no API key required)
- Implements caching to reduce API calls
- Handles rate limiting and network errors gracefully
- Provides offline fallback using coordinate formatting

## Data Models

### Core Data Structures

```csharp
public class PhotoFile
{
    public string FilePath { get; set; }
    public string FileName { get; set; }
    public long FileSize { get; set; }
    public DateTime FileCreated { get; set; }
    public PhotoMetadata Metadata { get; set; }
    public List<FilenameSuggestion> Suggestions { get; set; }
    public string SelectedSuggestion { get; set; }
    public bool IsSelected { get; set; }
}

public class GpsCoordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
}

public class CameraInfo
{
    public string Make { get; set; }
    public string Model { get; set; }
    public string LensModel { get; set; }
    public string Settings { get; set; } // ISO, aperture, shutter speed
}

public class NamingPattern
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Template { get; set; }
    public string Description { get; set; }
    public bool RequiresLocation { get; set; }
}
```

### ViewModels for MVVM Pattern

```csharp
public class MainViewModel : ViewModelBase
{
    public ObservableCollection<PhotoFile> PhotoFiles { get; set; }
    public NamingPattern SelectedPattern { get; set; }
    public string SelectedFolder { get; set; }
    public bool IsProcessing { get; set; }
    public int ProcessingProgress { get; set; }
    
    // Commands
    public ICommand SelectFolderCommand { get; set; }
    public ICommand GenerateSuggestionsCommand { get; set; }
    public ICommand ApplyRenamesCommand { get; set; }
    public ICommand UndoCommand { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Now I need to analyze the acceptance criteria to determine which ones can be tested as properties:

Based on the prework analysis, I'll convert the testable acceptance criteria into properties:

**Property 1: Comprehensive metadata extraction**
*For any* supported image file with metadata, the Metadata_Reader should extract all available information (date/time, GPS coordinates, camera details) and provide file system fallback when EXIF data is unavailable
**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

**Property 2: File discovery completeness**
*For any* directory containing image files, the Photo_Renamer should discover all supported image file types and accurately count them, including subdirectories when recursive scanning is enabled
**Validates: Requirements 1.1, 1.2, 1.4**

**Property 3: Filename suggestion generation**
*For any* photo with metadata, the Suggestion_Engine should generate multiple valid naming pattern suggestions based on available data (date, location, camera information)
**Validates: Requirements 3.1, 3.2, 3.4**

**Property 4: Windows filename validation**
*For any* generated filename suggestion, it should be valid for the Windows file system (no invalid characters, within path length limits, properly sanitized)
**Validates: Requirements 3.5, 7.1, 7.4**

**Property 5: Conflict resolution consistency**
*For any* set of photos that would generate identical filenames, the system should resolve conflicts by appending unique identifiers and prevent duplicate filename creation
**Validates: Requirements 3.3, 5.2, 7.3**

**Property 6: File operation safety**
*For any* file rename operation, the File_Manager should preserve original timestamps and attributes, maintain undo capability, and handle failures gracefully with specific error reporting
**Validates: Requirements 5.1, 5.3, 5.4**

**Property 7: UI information display consistency**
*For any* photo being processed, the User_Interface should display original filename, proposed filename, metadata information, and thumbnails in a consistent format
**Validates: Requirements 2.5, 4.1, 6.2**

**Property 8: Operation summary accuracy**
*For any* batch rename operation, the system should provide accurate summaries of files to be renamed before execution and results after completion
**Validates: Requirements 4.5, 5.5**

**Property 9: User preference persistence**
*For any* user configuration (naming patterns, folder locations), the system should save and restore these preferences between application sessions
**Validates: Requirements 6.5**

**Property 10: Progress indication during bulk operations**
*For any* operation processing multiple files, the User_Interface should display progress indicators that accurately reflect the current processing state
**Validates: Requirements 6.3**

## Error Handling

The application implements comprehensive error handling at multiple levels:

### Metadata Extraction Errors
- **Corrupted files**: Graceful fallback to file system dates
- **Unsupported formats**: Clear error messages with format recommendations
- **Permission issues**: Specific guidance for resolving access problems

### File Operation Errors
- **Permission denied**: Detailed error messages with resolution steps
- **Disk space**: Pre-operation validation and clear error reporting
- **File locks**: Retry mechanisms with user notification
- **Path length limits**: Validation before operation with alternative suggestions

### Network Errors (Geocoding)
- **API unavailable**: Graceful degradation to coordinate display
- **Rate limiting**: Automatic retry with exponential backoff
- **Invalid coordinates**: Fallback to raw coordinate display

### UI Error Handling
- **Invalid user input**: Real-time validation with helpful error messages
- **Application crashes**: Automatic recovery with operation history preservation
- **Memory issues**: Progress monitoring with batch size adjustment

## Testing Strategy

The application will use a dual testing approach combining unit tests and property-based tests for comprehensive coverage.

### Unit Testing Approach
Unit tests will focus on:
- **Specific examples**: Test known good inputs and expected outputs
- **Edge cases**: Empty folders, corrupted files, permission issues
- **Integration points**: Component interactions and data flow
- **UI behavior**: User interactions and display logic

**Testing Framework**: MSTest with FluentAssertions for readable assertions

### Property-Based Testing Approach
Property tests will verify universal properties across all inputs:
- **Metadata extraction**: Test with randomly generated image files
- **Filename generation**: Test with various metadata combinations
- **File operations**: Test with different file system scenarios
- **Conflict resolution**: Test with duplicate filename scenarios

**Testing Framework**: FsCheck.NET for property-based testing
**Configuration**: Minimum 100 iterations per property test
**Tagging**: Each test tagged with format: **Feature: photo-metadata-renamer, Property {number}: {property_text}**

### Test Data Strategy
- **Sample images**: Curated set of photos with various metadata scenarios
- **Generated test data**: Programmatically created files for edge case testing
- **Mock services**: Isolated testing of components without external dependencies
- **Integration testing**: End-to-end scenarios with real file operations

### Continuous Integration
- **Automated testing**: All tests run on every commit
- **Performance benchmarks**: Track metadata extraction and file operation performance
- **UI testing**: Automated UI tests for critical user workflows
- **Code coverage**: Maintain >90% code coverage with meaningful tests

The testing strategy ensures both specific correctness (unit tests) and general correctness (property tests), providing confidence in the application's reliability across all usage scenarios.