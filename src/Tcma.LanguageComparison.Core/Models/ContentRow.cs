namespace Tcma.LanguageComparison.Core.Models
{
    /// <summary>
    /// Represents a row from the CSV file containing localization content
    /// </summary>
    public record ContentRow
    {
        /// <summary>
        /// Unique identifier for the content element
        /// </summary>
        public string ContentId { get; init; } = string.Empty;

        /// <summary>
        /// The actual content text (may contain HTML)
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Index of this row in the original file (for tracking purposes)
        /// </summary>
        public int OriginalIndex { get; init; }

        /// <summary>
        /// Clean text version (HTML stripped) for embedding generation
        /// </summary>
        public string CleanContent { get; set; } = string.Empty;

        /// <summary>
        /// Generated embedding vector for this content
        /// </summary>
        public float[]? EmbeddingVector { get; set; }
    }
} 