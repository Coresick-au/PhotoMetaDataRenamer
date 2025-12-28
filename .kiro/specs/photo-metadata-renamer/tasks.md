# Implementation Plan: Photo Metadata Renamer

## Overview

Implementation of a Windows WPF application that extracts metadata from photos and provides intelligent renaming suggestions. The implementation follows MVVM architecture with clean separation of concerns and comprehensive testing.

## Tasks

- [x] 1. Set up project structure and dependencies
  - Create WPF .NET 8 project with MVVM structure
  - Add NuGet packages: MetadataExtractor, CommunityToolkit.Mvvm, MaterialDesignThemes
  - Set up folder structure for Models, ViewModels, Views, Services
  - Configure dependency injection container
  - _Requirements: All requirements (foundation)_

- [x] 2. Implement core data models and interfaces
  - [x] 2.1 Create PhotoMetadata, PhotoFile, and supporting data models
    - Define PhotoMetadata class with date, GPS, camera info properties
    - Define PhotoFile class with file path, metadata, and suggestions
    - Create GpsCoordinates, CameraInfo, LocationInfo classes
    - _Requirements: 2.1, 2.2, 2.3_

  - [ ] 2.2 Write property test for data model validation
    - **Property 1: Comprehensive metadata extraction**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**

  - [x] 2.3 Define service interfaces (IMetadataReader, ISuggestionEngine, IFileManager, IGeocodingService)
    - Create interface definitions with method signatures
    - Define return types and parameter objects
    - _Requirements: 2.1, 3.1, 5.1_

- [x] 3. Implement metadata extraction service
  - [x] 3.1 Create MetadataReader service using MetadataExtractor.NET
    - Implement EXIF date/time extraction
    - Implement GPS coordinate extraction
    - Implement camera information extraction
    - Add fallback to file system dates when EXIF unavailable
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [ ] 3.2 Write property test for metadata extraction
    - **Property 1: Comprehensive metadata extraction**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4**

  - [x] 3.3 Add support for multiple image formats
    - Handle JPEG, PNG, TIFF, and common RAW formats
    - Implement file type detection and validation
    - _Requirements: 1.1_

  - [ ] 3.4 Write unit tests for format support
    - Test supported file type detection
    - Test unsupported file handling
    - _Requirements: 1.1_

- [x] 4. Implement file discovery and scanning
  - [x] 4.1 Create folder scanning functionality
    - Implement recursive directory traversal
    - Filter for supported image file types
    - Count discovered files accurately
    - _Requirements: 1.1, 1.2, 1.4_

  - [ ] 4.2 Write property test for file discovery
    - **Property 2: File discovery completeness**
    - **Validates: Requirements 1.1, 1.2, 1.4_

- [x] 5. Implement geocoding service
  - [x] 5.1 Create geocoding service using OpenStreetMap Nominatim API
    - Implement reverse geocoding from GPS coordinates
    - Add caching mechanism to reduce API calls
    - Handle rate limiting and network errors
    - _Requirements: 3.2_

  - [ ] 5.2 Write unit tests for geocoding service
    - Test API integration with mock responses
    - Test caching behavior
    - Test error handling scenarios
    - _Requirements: 3.2_

- [x] 6. Implement suggestion engine
  - [x] 6.1 Create filename suggestion generation
    - Implement date-based naming patterns (YYYY-MM-DD, YYYYMMDD)
    - Implement location-based naming patterns
    - Implement camera-based naming patterns
    - Add multiple pattern options per photo
    - _Requirements: 3.1, 3.2, 3.4_

  - [ ] 6.2 Write property test for suggestion generation
    - **Property 3: Filename suggestion generation**
    - **Validates: Requirements 3.1, 3.2, 3.4**

  - [x] 6.3 Implement filename validation and sanitization
    - Remove invalid Windows filename characters
    - Validate path length limits
    - Ensure unique filenames with conflict resolution
    - _Requirements: 3.5, 7.1, 7.4_

  - [ ] 6.4 Write property test for filename validation
    - **Property 4: Windows filename validation**
    - **Validates: Requirements 3.5, 7.1, 7.4**

  - [x] 6.5 Implement conflict resolution
    - Detect duplicate filename scenarios
    - Append sequence numbers for conflicts
    - Prevent duplicate filename creation
    - _Requirements: 3.3, 5.2, 7.3_

  - [ ] 6.6 Write property test for conflict resolution
    - **Property 5: Conflict resolution consistency**
    - **Validates: Requirements 3.3, 5.2, 7.3**

- [x] 7. Checkpoint - Core services complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement file management service
  - [x] 8.1 Create safe file rename operations
    - Implement atomic file rename with rollback
    - Preserve file timestamps and attributes
    - Maintain operation history for undo functionality
    - _Requirements: 5.1, 5.4_

  - [ ] 8.2 Write property test for file operations
    - **Property 6: File operation safety**
    - **Validates: Requirements 5.1, 5.3, 5.4**

  - [ ] 8.3 Implement error handling and reporting
    - Handle permission errors with specific guidance
    - Report file operation failures with details
    - Log errors for troubleshooting
    - _Requirements: 5.3, 7.2, 7.5_

- [x] 9. Create main window and MVVM structure
  - [x] 9.1 Create MainWindow XAML with Material Design
    - Design folder selection area
    - Create photo list view with thumbnails
    - Add suggestion preview area
    - Include progress indicators and status bar
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 9.2 Implement MainViewModel with commands
    - Create SelectFolderCommand for folder selection
    - Create GenerateSuggestionsCommand for processing
    - Create ApplyRenamesCommand for file operations
    - Create UndoCommand for operation reversal
    - Implement progress tracking and status updates
    - _Requirements: 4.1, 4.3, 4.4, 4.5_

  - [ ] 9.3 Write property test for UI information display
    - **Property 7: UI information display consistency**
    - **Validates: Requirements 2.5, 4.1, 6.2**

- [ ] 10. Implement photo list and preview functionality
  - [ ] 10.1 Create photo list item user control
    - Display thumbnail, original filename, metadata
    - Show suggested filenames with pattern options
    - Add selection checkboxes and edit capabilities
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 10.2 Implement thumbnail generation
    - Generate thumbnails from image files
    - Cache thumbnails for performance
    - Handle thumbnail generation errors
    - _Requirements: 6.2_

  - [ ] 10.3 Write unit tests for UI components
    - Test photo list item display
    - Test thumbnail generation
    - Test user interaction handling
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.2_

- [x] 11. Implement batch operations and progress tracking
  - [x] 11.1 Create batch processing workflow
    - Process multiple photos asynchronously
    - Update progress indicators during processing
    - Handle cancellation and error scenarios
    - _Requirements: 6.3_

  - [ ] 11.2 Write property test for progress indication
    - **Property 10: Progress indication during bulk operations**
    - **Validates: Requirements 6.3**

  - [x] 11.3 Implement operation summaries
    - Show summary before applying changes
    - Display results after completion
    - Include success and failure counts
    - _Requirements: 4.5, 5.5_

  - [ ] 11.4 Write property test for operation summaries
    - **Property 8: Operation summary accuracy**
    - **Validates: Requirements 4.5, 5.5**

- [x] 12. Add user preferences and settings
  - [x] 12.1 Create settings persistence
    - Save naming pattern preferences
    - Remember last used folder locations
    - Store window size and position
    - _Requirements: 6.5_

  - [ ] 12.2 Write property test for preference persistence
    - **Property 9: User preference persistence**
    - **Validates: Requirements 6.5**

  - [ ] 12.3 Add keyboard shortcuts
    - Implement common operation shortcuts
    - Add shortcut indicators to UI
    - _Requirements: 6.4_

- [x] 13. Final integration and polish
  - [x] 13.1 Wire all components together
    - Configure dependency injection
    - Connect ViewModels to Services
    - Ensure proper error propagation
    - _Requirements: All requirements_

  - [ ] 13.2 Write integration tests
    - Test end-to-end workflows
    - Test error scenarios across components
    - Test performance with large file sets
    - _Requirements: All requirements_

  - [ ] 13.3 Add application icon and packaging
    - Create application icon and resources
    - Configure build output and deployment
    - Add application manifest and version info
    - _Requirements: 6.1_

- [ ] 14. Final checkpoint - Complete application
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks include comprehensive testing from the start for robust development
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The implementation uses WPF with .NET 8 and follows MVVM architecture
- MetadataExtractor.NET library handles EXIF data extraction
- OpenStreetMap Nominatim API provides geocoding services