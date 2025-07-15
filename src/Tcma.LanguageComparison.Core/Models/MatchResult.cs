namespace Tcma.LanguageComparison.Core.Models
{
    /// <summary>
    /// Represents the result of matching between reference and target content
    /// </summary>
    public record MatchResult
    {
        /// <summary>
        /// Reference content row (e.g., English)
        /// </summary>
        public ContentRow ReferenceRow { get; init; } = null!;

        /// <summary>
        /// Best matching target content row (e.g., Korean)
        /// </summary>
        public ContentRow? MatchedRow { get; init; }

        /// <summary>
        /// Cosine similarity score between the two contents (0-1, higher is better)
        /// </summary>
        public double SimilarityScore { get; init; }

        /// <summary>
        /// Indicates if this is considered a good match based on threshold
        /// </summary>
        public bool IsGoodMatch { get; init; }

        /// <summary>
        /// Match quality based on similarity score
        /// </summary>
        public MatchQuality Quality => SimilarityScore switch
        {
            >= 0.8 => MatchQuality.High,
            >= 0.6 => MatchQuality.Medium,
            >= 0.4 => MatchQuality.Low,
            _ => MatchQuality.Poor
        };
    }

    /// <summary>
    /// Enumeration representing the quality of a match
    /// </summary>
    public enum MatchQuality
    {
        Poor,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Represents line-by-line match result with suggestion
    /// </summary>
    public record LineByLineMatchResult
    {
        public ContentRow TargetRow { get; init; } = null!;
        public ContentRow? CorrespondingReferenceRow { get; init; }
        public double LineByLineScore { get; init; }
        public bool IsGoodLineByLineMatch { get; init; }
        public MatchResult? SuggestedMatch { get; init; }
        public MatchQuality Quality => LineByLineScore switch
        {
            >= 0.8 => MatchQuality.High,
            >= 0.6 => MatchQuality.Medium,
            >= 0.4 => MatchQuality.Low,
            _ => MatchQuality.Poor
        };
    }

    /// <summary>
    /// Statistics about matching results
    /// </summary>
    public record MatchingStatistics
    {
        public int TotalReferenceRows { get; init; }
        public int GoodMatches { get; init; }
        public int HighQualityMatches { get; init; }
        public int MediumQualityMatches { get; init; }
        public int LowQualityMatches { get; init; }
        public int PoorQualityMatches { get; init; }
        public double MatchPercentage { get; init; }
        public double AverageSimilarityScore { get; init; }
    }
} 