# TCMA Language Comparison Tool - Algorithm Documentation

## Overview

The TCMA Language Comparison Tool implements a sophisticated AI-driven approach to compare and align localization content between different languages. This document provides a comprehensive explanation of the algorithms, technologies, and methodologies employed in the core processing pipeline.

## Core Technologies Stack

### 1. Google Gemini AI API
- **Purpose**: Generate high-quality text embeddings for semantic understanding
- **Model**: Google's Gemini text embedding model
- **API Endpoint**: `https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent`
- **Embedding Dimension**: 768-dimensional dense vectors

### 2. Text Preprocessing Pipeline
- **HTML Tag Removal**: Uses HtmlAgilityPack to clean HTML content
- **Normalization**: Standardizes text format for consistent embedding generation
- **Content Validation**: Ensures text length constraints (max 8000 characters per API limits)

### 3. Cosine Similarity Algorithm
- **Mathematical Foundation**: Measures angular similarity between vector embeddings
- **Range**: [-1, 1] where 1 indicates identical semantic meaning
- **Implementation**: Optimized dot product calculation with magnitude normalization

## Algorithm Architecture

### Phase 1: Text Preprocessing

```
Input Text → HTML Cleaning → Normalization → Validation → Clean Text
```

**Process Details:**
1. **HTML Tag Removal**: Strip all HTML tags while preserving text content
2. **Whitespace Normalization**: Remove extra spaces and normalize line breaks
3. **Length Validation**: Ensure content fits within API constraints
4. **Character Encoding**: Handle Unicode characters properly

### Phase 2: Embedding Generation

```
Clean Text → API Request → Vector Embedding (768-dim) → Normalized Vector
```

**Key Features:**
- **Batch Processing**: Groups multiple texts to optimize API calls
- **Rate Limiting**: Implements exponential backoff to respect API quotas
- **Error Handling**: Robust retry logic for transient failures
- **Vector Normalization**: Ensures unit vectors for accurate similarity calculations

**API Request Structure:**
```json
{
  "model": "models/text-embedding-004",
  "content": {
    "parts": [{"text": "content to embed"}]
  }
}
```

### Phase 3: Similarity Calculation

The core algorithm uses **Cosine Similarity** to measure semantic similarity between text embeddings:

#### Mathematical Formula

```
cosine_similarity(A, B) = (A · B) / (||A|| × ||B||)
```

Where:
- `A · B` = dot product of vectors A and B
- `||A||` = magnitude (Euclidean norm) of vector A
- `||B||` = magnitude (Euclidean norm) of vector B

#### Implementation Details

```csharp
public static float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
{
    if (vectorA.Length != vectorB.Length)
        throw new ArgumentException("Vectors must have the same length");

    float dotProduct = 0f;
    float magnitudeA = 0f;
    float magnitudeB = 0f;

    for (int i = 0; i < vectorA.Length; i++)
    {
        dotProduct += vectorA[i] * vectorB[i];
        magnitudeA += vectorA[i] * vectorA[i];
        magnitudeB += vectorB[i] * vectorB[i];
    }

    magnitudeA = (float)Math.Sqrt(magnitudeA);
    magnitudeB = (float)Math.Sqrt(magnitudeB);

    if (magnitudeA == 0f || magnitudeB == 0f)
        return 0f;

    return dotProduct / (magnitudeA * magnitudeB);
}
```

### Phase 4: Line-by-Line Matching Algorithm

The system implements a **Line-by-Line Comparison Strategy** that maintains the original order of content while providing intelligent suggestions:

#### Core Algorithm Flow

```
1. For each target line (Ti):
   a. Calculate similarity with corresponding reference line (Ri)
   b. If similarity >= threshold:
      - Mark as direct match
   c. If similarity < threshold:
      - Find best match from entire reference dataset
      - Provide as suggestion
   d. Classify quality based on similarity score

2. Preserve original line order in output
3. Generate comprehensive statistics
```

#### Quality Classification System

```
- High Quality (Green):   similarity >= 0.8
- Medium Quality (Yellow): 0.6 <= similarity < 0.8  
- Low Quality (Pink):     0.4 <= similarity < 0.6
- Poor Quality (White):   similarity < 0.4
```

## Advanced Features

### 1. Intelligent Suggestion System

When line-by-line similarity falls below the threshold, the algorithm:

1. **Global Search**: Scans entire reference dataset
2. **Best Match Selection**: Identifies highest similarity score
3. **Contextual Recommendation**: Provides as alternative suggestion
4. **Quality Assessment**: Evaluates suggestion reliability

### 2. Batch Processing Optimization

**API Efficiency Strategy:**
- **Chunked Requests**: Groups multiple texts per API call
- **Concurrent Processing**: Parallel embedding generation
- **Rate Limiting**: Adaptive delays to prevent quota exhaustion
- **Retry Logic**: Exponential backoff for failed requests

### 3. Error Handling and Resilience

**Multi-Layer Error Management:**
- **Network Resilience**: Automatic retry with exponential backoff
- **Data Validation**: Comprehensive input validation
- **Graceful Degradation**: Fallback strategies for partial failures
- **User Feedback**: Real-time progress and error reporting

## Performance Characteristics

### Computational Complexity

- **Embedding Generation**: O(n) where n = number of text segments
- **Similarity Calculation**: O(d) where d = embedding dimension (768)
- **Matching Algorithm**: O(n²) for suggestion finding, O(n) for line-by-line
- **Overall Complexity**: O(n² + API_latency)

### Scalability Considerations

- **Memory Usage**: Linear with dataset size
- **API Rate Limits**: Configurable batch sizes and delays
- **Processing Time**: Dominated by API response times
- **Concurrent Requests**: Balanced for optimal throughput

## Quality Metrics and Statistics

### Matching Statistics

The system provides comprehensive analytics:

```csharp
public class MatchingStatistics
{
    public int TotalRows { get; set; }
    public int HighQualityMatches { get; set; }    // >= 0.8
    public int MediumQualityMatches { get; set; }  // 0.6-0.8
    public int LowQualityMatches { get; set; }     // 0.4-0.6
    public int PoorQualityMatches { get; set; }    // < 0.4
    public double AverageSimilarity { get; set; }
    public double MatchPercentage { get; set; }
}
```

### Embedding Processing Statistics

```csharp
public record EmbeddingProcessingStats
{
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
    public int RetryAttempts { get; init; }
    public double SuccessRate => TotalRequests > 0 ? 
        (double)SuccessfulRequests / TotalRequests * 100 : 0;
}
```

## Advantages of the Chosen Approach

### 1. Semantic Understanding
- **Beyond Literal Matching**: Understands meaning rather than exact word matches
- **Context Awareness**: Considers surrounding context for better matching
- **Language Flexibility**: Works across different languages and writing styles

### 2. Scalability and Performance
- **API Efficiency**: Optimized request batching reduces API costs
- **Parallel Processing**: Concurrent operations improve throughput
- **Memory Efficient**: Streaming processing for large datasets

### 3. User Experience
- **Real-time Feedback**: Progress indicators and status updates
- **Quality Visualization**: Color-coded quality assessment
- **Intelligent Suggestions**: Provides alternatives when direct matches fail

### 4. Robustness
- **Error Recovery**: Comprehensive error handling and retry mechanisms
- **Data Integrity**: Maintains original content order and structure
- **Fault Tolerance**: Graceful handling of API failures and network issues


## Conclusion

The TCMA Language Comparison Tool demonstrates a sophisticated application of modern AI technologies for practical localization challenges. By combining Google's advanced embedding models with optimized similarity algorithms and robust error handling, the system provides accurate, scalable, and user-friendly content comparison capabilities.

The line-by-line matching approach ensures content integrity while the intelligent suggestion system provides valuable alternatives for challenging translations. The comprehensive error handling and performance optimizations make the tool suitable for production environments with varying data quality and network conditions. 