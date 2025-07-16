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

    /// <summary>
    /// Kết quả align target với reference (cho export file target mới)
    /// </summary>
    public record AlignedTargetResult
    {
        public List<AlignedTargetRow> AlignedRows { get; init; } = new();
        public List<ContentRow> UnusedTargetRows { get; init; } = new();
        public int TotalReferenceRows { get; init; }
        public int MatchedRows { get; init; }
        public int MissingRows { get; init; }
        public int UnusedRows { get; init; }
    }

    /// <summary>
    /// Một dòng align giữa reference và target (target có thể null nếu thiếu)
    /// </summary>
    public record AlignedTargetRow
    {
        public int ReferenceIndex { get; init; }
        public ContentRow? TargetRow { get; init; }  // null nếu không có match
        public double? SimilarityScore { get; init; }
        public bool HasMatch => TargetRow != null;
        public string Status => HasMatch ? "Matched" : "Missing";
    }

    /// <summary>
    /// Model cho việc hiển thị aligned data trong DataGrid (cả single và multiple page mode)
    /// </summary>
    public record AlignedDisplayRow
    {
        /// <summary>
        /// Số dòng reference (1-based)
        /// </summary>
        public int RefLineNumber { get; init; }
        
        /// <summary>
        /// Nội dung reference
        /// </summary>
        public string RefContent { get; init; } = string.Empty;
        
        /// <summary>
        /// Số dòng target (1-based), null nếu không có match
        /// </summary>
        public int? TargetLineNumber { get; init; }
        
        /// <summary>
        /// Nội dung target, empty nếu không có match
        /// </summary>
        public string TargetContent { get; init; } = string.Empty;
        
        /// <summary>
        /// ContentId của target, empty nếu không có match
        /// </summary>
        public string TargetContentId { get; init; } = string.Empty;
        
        /// <summary>
        /// Trạng thái: "Matched" hoặc "Missing"
        /// </summary>
        public string Status { get; init; } = "Missing";
        
        /// <summary>
        /// Điểm similarity (0-1), null nếu không có match
        /// </summary>
        public double? SimilarityScore { get; init; }
        
        /// <summary>
        /// Quality cho styling (High, Medium, Low, Poor)
        /// </summary>
        public MatchQuality Quality { get; init; } = MatchQuality.Poor;
        
        /// <summary>
        /// Background color cho row styling
        /// </summary>
        public string RowBackground => Quality switch
        {
            MatchQuality.High => "#E8F5E8",      // Light green
            MatchQuality.Medium => "#FFF3E0",    // Light orange
            MatchQuality.Low => "#FFEBEE",       // Light red
            MatchQuality.Poor => "#FAFAFA",      // Light gray
            _ => "White"
        };
        
        /// <summary>
        /// Tạo từ AlignedTargetRow
        /// </summary>
        public static AlignedDisplayRow FromAlignedTargetRow(AlignedTargetRow alignedRow, ContentRow referenceRow)
        {
            return new AlignedDisplayRow
            {
                RefLineNumber = alignedRow.ReferenceIndex + 1,  // Convert to 1-based
                RefContent = referenceRow.Content,
                TargetLineNumber = alignedRow.TargetRow?.OriginalIndex + 1,  // Convert to 1-based, null if no match
                TargetContent = alignedRow.TargetRow?.Content ?? string.Empty,
                TargetContentId = alignedRow.TargetRow?.ContentId ?? string.Empty,
                Status = alignedRow.Status,
                SimilarityScore = alignedRow.SimilarityScore,
                Quality = alignedRow.SimilarityScore switch
                {
                    >= 0.8 => MatchQuality.High,
                    >= 0.6 => MatchQuality.Medium,
                    >= 0.4 => MatchQuality.Low,
                    _ => MatchQuality.Poor
                }
            };
        }
    }
} 