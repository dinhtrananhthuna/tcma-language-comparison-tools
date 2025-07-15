namespace Tcma.LanguageComparison.Core.Models
{
    /// <summary>
    /// Application configuration loaded from appsettings.json
    /// </summary>
    public class AppConfiguration
    {
        public LanguageComparisonSettings LanguageComparison { get; set; } = new();
        public OutputSettings Output { get; set; } = new();
        public PreprocessingSettings Preprocessing { get; set; } = new();
    }

    /// <summary>
    /// Language comparison specific settings
    /// </summary>
    public class LanguageComparisonSettings
    {
        /// <summary>
        /// Minimum similarity score to consider a good match (0.0 to 1.0)
        /// </summary>
        public double SimilarityThreshold { get; set; } = 0.5;

        /// <summary>
        /// Maximum number of items to process in a single batch for embedding generation
        /// </summary>
        public int MaxEmbeddingBatchSize { get; set; } = 50;

        /// <summary>
        /// Maximum number of concurrent API requests
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 5;

        /// <summary>
        /// Minimum content length for processing
        /// </summary>
        public int MinContentLength { get; set; } = 3;

        /// <summary>
        /// Maximum content length for API calls
        /// </summary>
        public int MaxContentLength { get; set; } = 8000;

        /// <summary>
        /// Number of rows to limit for demo purposes (0 = no limit)
        /// </summary>
        public int DemoRowLimit { get; set; } = 10;
    }

    /// <summary>
    /// Output and display settings
    /// </summary>
    public class OutputSettings
    {
        /// <summary>
        /// Whether to show detailed match results
        /// </summary>
        public bool ShowDetailedResults { get; set; } = true;

        /// <summary>
        /// Whether to show progress messages during processing
        /// </summary>
        public bool ShowProgressMessages { get; set; } = true;

        /// <summary>
        /// Whether to export unmatched items as placeholder rows
        /// </summary>
        public bool ExportUnmatchedAsPlaceholder { get; set; } = true;
    }

    /// <summary>
    /// Text preprocessing settings
    /// </summary>
    public class PreprocessingSettings
    {
        /// <summary>
        /// Whether to strip HTML tags from content
        /// </summary>
        public bool StripHtmlTags { get; set; } = true;

        /// <summary>
        /// Whether to normalize whitespace in content
        /// </summary>
        public bool NormalizeWhitespace { get; set; } = true;

        /// <summary>
        /// Whether to remove special characters from content
        /// </summary>
        public bool RemoveSpecialCharacters { get; set; } = true;
    }
} 