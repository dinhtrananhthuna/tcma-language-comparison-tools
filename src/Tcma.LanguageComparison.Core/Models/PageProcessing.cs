using System;
using System.Collections.Generic;

namespace Tcma.LanguageComparison.Core.Models
{
    /// <summary>
    /// Status of a page in the multi-page processing workflow
    /// </summary>
    public enum PageStatus
    {
        /// <summary>
        /// Files are paired and ready to be processed
        /// </summary>
        Ready,
        
        /// <summary>
        /// Page is currently being processed
        /// </summary>
        Processing,
        
        /// <summary>
        /// Page has been successfully processed
        /// </summary>
        Completed,
        
        /// <summary>
        /// An error occurred during processing
        /// </summary>
        Error,
        
        /// <summary>
        /// Results are cached in memory
        /// </summary>
        Cached
    }

    /// <summary>
    /// Information about a page pair in multi-page processing
    /// </summary>
    public class PageInfo
    {
        /// <summary>
        /// Name of the page (extracted from filename pattern)
        /// </summary>
        public string PageName { get; set; } = string.Empty;
        
        /// <summary>
        /// Full path to the reference file
        /// </summary>
        public string ReferenceFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Full path to the target file
        /// </summary>
        public string TargetFilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// Current status of this page
        /// </summary>
        public PageStatus Status { get; set; } = PageStatus.Ready;
        
        /// <summary>
        /// When this page was last processed
        /// </summary>
        public DateTime? LastProcessed { get; set; }
        
        /// <summary>
        /// Error message if status is Error
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Whether results are currently cached in memory
        /// </summary>
        public bool IsResultsCached { get; set; }
        
        /// <summary>
        /// Processing progress (0-100)
        /// </summary>
        public int Progress { get; set; }
        
        /// <summary>
        /// Gets the display name for this page
        /// </summary>
        public string DisplayName => $"{PageName} ({System.IO.Path.GetFileName(ReferenceFilePath)} ↔ {System.IO.Path.GetFileName(TargetFilePath)})";
        
        /// <summary>
        /// Gets the status icon for UI display
        /// </summary>
        public string StatusIcon => Status switch
        {
            PageStatus.Ready => "📄",
            PageStatus.Processing => "⏳",
            PageStatus.Completed => "✅",
            PageStatus.Error => "❌",
            PageStatus.Cached => "💾",
            _ => "📄"
        };
        
        /// <summary>
        /// Whether this page can be processed
        /// </summary>
        public bool CanProcess => Status == PageStatus.Ready || Status == PageStatus.Error;
        
        /// <summary>
        /// Whether this page is currently being processed (for UI binding)
        /// </summary>
        public bool IsProcessing => Status == PageStatus.Processing;
    }

    /// <summary>
    /// Results of processing a single page
    /// </summary>
    public class PageMatchingResult
    {
        /// <summary>
        /// Information about the processed page
        /// </summary>
        public PageInfo PageInfo { get; set; } = new();
        
        /// <summary>
        /// Line-by-line comparison results
        /// </summary>
        public List<LineByLineMatchResult> Results { get; set; } = new();
        
        /// <summary>
        /// When this page was processed
        /// </summary>
        public DateTime ProcessedAt { get; set; }
        
        /// <summary>
        /// Processing statistics
        /// </summary>
        public PageStatistics Statistics { get; set; } = new();
        
        /// <summary>
        /// Whether the processing was successful
        /// </summary>
        public bool IsSuccess => PageInfo.Status == PageStatus.Completed;
    }

    /// <summary>
    /// Statistics for a processed page
    /// </summary>
    public class PageStatistics
    {
        /// <summary>
        /// Total number of reference rows
        /// </summary>
        public int TotalReferenceRows { get; set; }
        
        /// <summary>
        /// Total number of target rows
        /// </summary>
        public int TotalTargetRows { get; set; }
        
        /// <summary>
        /// Number of good matches found
        /// </summary>
        public int GoodMatches { get; set; }
        
        /// <summary>
        /// Average similarity score
        /// </summary>
        public double AverageSimilarityScore { get; set; }
        
        /// <summary>
        /// Match percentage
        /// </summary>
        public double MatchPercentage => TotalReferenceRows > 0 
            ? (double)GoodMatches / TotalReferenceRows * 100 
            : 0;
        
        /// <summary>
        /// Processing duration
        /// </summary>
        public TimeSpan ProcessingDuration { get; set; }
    }

    /// <summary>
    /// Result of extracting and pairing files from ZIP archives
    /// </summary>
    public class FileExtractionResult
    {
        /// <summary>
        /// List of successfully paired pages
        /// </summary>
        public List<PageInfo> Pages { get; set; } = new();
        
        /// <summary>
        /// Files that couldn't be paired
        /// </summary>
        public List<string> UnpairedFiles { get; set; } = new();
        
        /// <summary>
        /// Temporary directory where files were extracted
        /// </summary>
        public string ExtractionDirectory { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the extraction was successful
        /// </summary>
        public bool IsSuccess => Pages.Count > 0;
        
        /// <summary>
        /// Summary of extraction results
        /// </summary>
        public string Summary => $"Found {Pages.Count} page pairs, {UnpairedFiles.Count} unpaired files";
    }
} 