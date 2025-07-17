# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TCMA Language Comparison Tool is a C# .NET 8 Windows application that uses AI embeddings to compare and align localization content between different languages. The tool leverages Google Gemini API for semantic understanding and provides both console and GUI interfaces.

## Project Structure

- **src/Tcma.LanguageComparison.Core/**: Core library with business logic, services, and algorithms
- **src/Tcma.LanguageComparison.Gui/**: WPF Windows GUI application
- **src/Tcma.LanguageComparison.Core.Tests/**: MSTest unit tests
- **sample/**: Sample CSV files for testing (English/German language pairs)

## Common Development Commands

### Build Commands
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Tcma.LanguageComparison.Core
dotnet build src/Tcma.LanguageComparison.Gui
```

### Run Commands
```bash
# Run console application (requires GEMINI_API_KEY environment variable)
dotnet run --project src/Tcma.LanguageComparison.Core

# Run alignment test
dotnet run --project src/Tcma.LanguageComparison.Core test

# Run WPF GUI application
dotnet run --project src/Tcma.LanguageComparison.Gui
```

### Test Commands
```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run specific test project
dotnet test src/Tcma.LanguageComparison.Core.Tests
```

### Package Commands
```bash
# ZIP Distribution (Recommended - most reliable)
cd src/Tcma.LanguageComparison.Gui
powershell -ExecutionPolicy Bypass -File build-zip.ps1

# MSIX Package (Modern Windows installer)
powershell -ExecutionPolicy Bypass -File build-msix.ps1

# Inno Setup (Traditional installer - requires Inno Setup installed)
powershell -ExecutionPolicy Bypass -File build-inno.ps1
```

## Core Architecture

### Embedding-Based Matching Algorithm
The tool uses Google Gemini's text embedding API to generate 768-dimensional vectors representing semantic meaning of text content. Matches are found using cosine similarity calculations between embeddings.

### Key Services
- **GeminiEmbeddingService**: Handles API communication with Google Gemini
- **ContentMatchingService**: Core matching algorithm using line-by-line comparison
- **CsvReaderService**: CSV file parsing and validation
- **TextPreprocessingService**: HTML tag removal and text normalization

### Quality Classification
- High Quality (Green): similarity ≥ 0.8
- Medium Quality (Yellow): 0.6 ≤ similarity < 0.8  
- Low Quality (Pink): 0.4 ≤ similarity < 0.6
- Poor Quality (White): similarity < 0.4

## Configuration Requirements

### API Key Setup
The application requires a Google Gemini API key:
```bash
# Windows PowerShell
$env:GEMINI_API_KEY="your_api_key_here"

# Windows CMD
set GEMINI_API_KEY=your_api_key_here

# Linux/Mac
export GEMINI_API_KEY="your_api_key_here"
```

### CSV File Format
Expected CSV format:
```csv
ContentId,Content
ID001,"Sample content with possible HTML tags"
ID002,"Another piece of content"
```

## Error Handling Architecture

The codebase implements a comprehensive error handling system:
- **ErrorTypes.cs**: Centralized error classification with categories and severity levels
- **ErrorHandlingService**: Intelligent error processing with recovery guidance
- **OperationResult<T>**: Wrapper for success/failure handling

## Test Strategy

### Alignment Test
Run the built-in alignment test to verify algorithm accuracy:
```bash
dotnet run --project src/Tcma.LanguageComparison.Core test
```
Success criteria: ≥80% ContentId consistency between reference and target files.

### Unit Tests
Located in `src/Tcma.LanguageComparison.Core.Tests/` using MSTest framework.

## Technology Stack

- **.NET 8**: Target framework
- **WPF**: GUI framework for Windows application
- **Google Gemini API**: Text embedding generation
- **CsvHelper**: CSV parsing and generation
- **HtmlAgilityPack**: HTML tag removal
- **MSTest**: Testing framework

## Development Notes

### Cross-Language Matching
The tool is optimized for cross-language comparison with a low similarity threshold (0.35) to capture semantic similarity between different languages.

### Performance Considerations
- Batch processing for embedding generation
- Rate limiting to respect API quotas
- Concurrent processing where possible
- Memory-efficient streaming for large datasets

### Packaging
The GUI application supports multiple packaging methods:
- **ZIP Distribution**: Portable package that works everywhere (recommended)
- **MSIX Package**: Modern Windows installer with Microsoft Store compatibility
- **Inno Setup**: Traditional installer with Start menu integration