# TCMA Language Comparison Tool

A C# .NET 8 Windows application that uses Google Gemini AI embeddings to compare and align localization content between different languages through semantic similarity analysis.

## Features

- **AI-Powered Matching**: Uses Google Gemini text embeddings to find semantic similarities between translations
- **Multiple Comparison Modes**: Support for aligned line-by-line comparison and optimal content matching
- **Quality Classification**: Automatically categorizes matches into High, Medium, Low, and Poor quality ratings
- **Dual Interface**: Both console and WPF GUI applications available
- **Batch Processing**: Efficient processing of large CSV files with concurrent API calls
- **Export Functionality**: Generate aligned CSV outputs for further analysis

## Architecture

### Core Components

- **GeminiEmbeddingService**: Handles Google Gemini API communication with retry logic and rate limiting
- **ContentMatchingService**: Implements similarity matching algorithms with configurable thresholds
- **CsvReaderService**: Processes CSV files with proper validation and error handling
- **TextPreprocessingService**: Cleans content by removing HTML tags and normalizing text

### Matching Algorithm

The tool generates 768-dimensional embedding vectors for text content using Google Gemini's text embedding model. Content similarity is calculated using cosine similarity between embeddings, with configurable quality thresholds:

- **High Quality**: similarity ≥ 0.8 (Green)
- **Medium Quality**: 0.6 ≤ similarity < 0.8 (Yellow)
- **Low Quality**: 0.4 ≤ similarity < 0.6 (Pink)
- **Poor Quality**: similarity < 0.4 (White)

## Requirements

- .NET 8 SDK
- Google Gemini API key
- Windows OS (for GUI application)

## Installation

1. Clone the repository
2. Set up Google Gemini API key:
   ```powershell
   $env:GEMINI_API_KEY="your_api_key_here"
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```

## Usage

### Console Application

```bash
# Run standard comparison
dotnet run --project src/Tcma.LanguageComparison.Core

# Run alignment test
dotnet run --project src/Tcma.LanguageComparison.Core test
```

### GUI Application

```bash
dotnet run --project src/Tcma.LanguageComparison.Gui
```

### CSV File Format

Expected input format:
```csv
ContentId,Content
ID001,"Sample content with possible HTML tags"
ID002,"Another piece of content"
```

## Testing

Run unit tests:
```bash
dotnet test
```

Run built-in alignment test:
```bash
dotnet run --project src/Tcma.LanguageComparison.Core test
```

## Packaging

The application supports multiple distribution methods:

- **ZIP Package (Recommended)**:
  ```powershell
  cd src/Tcma.LanguageComparison.Gui
  powershell -ExecutionPolicy Bypass -File build-zip.ps1
  ```

- **MSIX Package**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File build-msix.ps1
  ```

- **Inno Setup**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File build-inno.ps1
  ```

## Configuration

Key settings can be configured in `appsettings.json`:

- `SimilarityThreshold`: Minimum similarity score for good matches (default: 0.35)
- `DemoRowLimit`: Limit processing for demo purposes
- API timeout and retry settings

## Error Handling

The application includes comprehensive error handling with:
- Categorized error types (API, Network, Validation, etc.)
- Severity levels (Critical, High, Medium, Low)
- User-friendly messages with suggested actions
- Automatic retry logic for transient failures

## Performance

- Concurrent processing with configurable limits
- Batch processing for large datasets
- Memory-efficient streaming
- Rate limiting to respect API quotas

## Technology Stack

- .NET 8
- WPF (Windows Presentation Foundation)
- Google Gemini API
- CsvHelper for CSV processing
- HtmlAgilityPack for HTML tag removal
- MSTest for unit testing

## License

This project is proprietary software developed by Vu Dinh - Simplify Dalat.