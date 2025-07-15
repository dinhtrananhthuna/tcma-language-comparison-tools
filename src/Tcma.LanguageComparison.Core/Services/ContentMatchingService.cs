using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for matching content between reference and target language files
    /// </summary>
    public class ContentMatchingService
    {
        private readonly double _similarityThreshold;

        /// <summary>
        /// Initializes the content matching service
        /// </summary>
        /// <param name="similarityThreshold">Minimum similarity score to consider a good match (0.0 to 1.0)</param>
        public ContentMatchingService(double similarityThreshold = 0.5)
        {
            if (similarityThreshold < 0.0 || similarityThreshold > 1.0)
            {
                throw new ArgumentException("Similarity threshold must be between 0.0 and 1.0", nameof(similarityThreshold));
            }

            _similarityThreshold = similarityThreshold;
        }

        /// <summary>
        /// Finds matches between reference and target content rows
        /// </summary>
        /// <param name="referenceRows">Reference language content (e.g., English)</param>
        /// <param name="targetRows">Target language content (e.g., Korean)</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>List of match results</returns>
        public async Task<List<MatchResult>> FindMatchesAsync(
            IEnumerable<ContentRow> referenceRows,
            IEnumerable<ContentRow> targetRows,
            IProgress<string>? progressCallback = null)
        {
            var refList = referenceRows.ToList();
            var targetList = targetRows.ToList();
            var results = new List<MatchResult>();

            progressCallback?.Report($"Bắt đầu tìm matches cho {refList.Count} reference rows với {targetList.Count} target rows...");

            // Validate that embeddings exist
            var refWithEmbeddings = refList.Where(r => r.EmbeddingVector != null).ToList();
            var targetWithEmbeddings = targetList.Where(t => t.EmbeddingVector != null).ToList();

            if (refWithEmbeddings.Count == 0)
            {
                throw new InvalidOperationException("No reference rows have embedding vectors. Please generate embeddings first.");
            }

            if (targetWithEmbeddings.Count == 0)
            {
                throw new InvalidOperationException("No target rows have embedding vectors. Please generate embeddings first.");
            }

            progressCallback?.Report($"Có {refWithEmbeddings.Count} reference và {targetWithEmbeddings.Count} target rows có embeddings.");

            // Keep track of used target rows to avoid duplicate matches
            var usedTargetRows = new HashSet<int>();
            var processed = 0;

            // For each reference row, find the best match in target rows
            foreach (var refRow in refWithEmbeddings)
            {
                var bestMatch = await FindBestMatchAsync(refRow, targetWithEmbeddings, usedTargetRows);
                results.Add(bestMatch);

                // Mark the matched target row as used (if found)
                if (bestMatch.MatchedRow != null && bestMatch.IsGoodMatch)
                {
                    usedTargetRows.Add(bestMatch.MatchedRow.OriginalIndex);
                }

                processed++;
                if (processed % 10 == 0 || processed == refWithEmbeddings.Count)
                {
                    progressCallback?.Report($"Đã xử lý {processed}/{refWithEmbeddings.Count} reference rows...");
                }
            }

            // Add unmatched reference rows
            var unmatchedRefRows = refList.Where(r => r.EmbeddingVector == null);
            foreach (var unmatchedRow in unmatchedRefRows)
            {
                results.Add(new MatchResult
                {
                    ReferenceRow = unmatchedRow,
                    MatchedRow = null,
                    SimilarityScore = 0.0,
                    IsGoodMatch = false
                });
            }

            progressCallback?.Report($"Hoàn thành! Tìm được {results.Count(r => r.IsGoodMatch)} matches tốt trong tổng số {results.Count} reference rows.");

            return results.OrderBy(r => r.ReferenceRow.OriginalIndex).ToList();
        }

        /// <summary>
        /// Finds the best match for a single reference row
        /// </summary>
        private async Task<MatchResult> FindBestMatchAsync(
            ContentRow referenceRow,
            IEnumerable<ContentRow> targetRows,
            HashSet<int> usedTargetRows)
        {
            if (referenceRow.EmbeddingVector == null)
            {
                return new MatchResult
                {
                    ReferenceRow = referenceRow,
                    MatchedRow = null,
                    SimilarityScore = 0.0,
                    IsGoodMatch = false
                };
            }

            double bestSimilarity = -1.0;
            ContentRow? bestMatch = null;

            // Check similarity with all available target rows
            foreach (var targetRow in targetRows)
            {
                // Skip if already used or doesn't have embedding
                if (usedTargetRows.Contains(targetRow.OriginalIndex) || targetRow.EmbeddingVector == null)
                    continue;

                var similarity = GeminiEmbeddingService.CalculateCosineSimilarity(
                    referenceRow.EmbeddingVector,
                    targetRow.EmbeddingVector);

                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = targetRow;
                }
            }

            return new MatchResult
            {
                ReferenceRow = referenceRow,
                MatchedRow = bestMatch,
                SimilarityScore = bestSimilarity,
                IsGoodMatch = bestSimilarity >= _similarityThreshold
            };
        }

        /// <summary>
        /// Creates a reordered list of target rows based on match results
        /// </summary>
        /// <param name="matchResults">Results from FindMatchesAsync</param>
        /// <returns>Target rows reordered to align with reference file</returns>
        public List<ContentRow> CreateReorderedTargetList(IEnumerable<MatchResult> matchResults)
        {
            var reorderedList = new List<ContentRow>();

            foreach (var result in matchResults.OrderBy(r => r.ReferenceRow.OriginalIndex))
            {
                if (result.MatchedRow != null && result.IsGoodMatch)
                {
                    reorderedList.Add(result.MatchedRow);
                }
                else
                {
                    // Create a placeholder row for unmatched content
                    reorderedList.Add(new ContentRow
                    {
                        ContentId = $"UNMATCHED_{result.ReferenceRow.ContentId}",
                        Content = $"[KHÔNG TÌM THẤY MATCH CHO: {result.ReferenceRow.Content}]",
                        OriginalIndex = -1
                    });
                }
            }

            return reorderedList;
        }

        /// <summary>
        /// Gets statistics about the matching results
        /// </summary>
        /// <param name="matchResults">Results from FindMatchesAsync</param>
        /// <returns>Matching statistics</returns>
        public MatchingStatistics GetMatchingStatistics(IEnumerable<MatchResult> matchResults)
        {
            var results = matchResults.ToList();
            var totalCount = results.Count;
            var goodMatches = results.Count(r => r.IsGoodMatch);
            var highQuality = results.Count(r => r.Quality == MatchQuality.High);
            var mediumQuality = results.Count(r => r.Quality == MatchQuality.Medium);
            var lowQuality = results.Count(r => r.Quality == MatchQuality.Low);
            var poorQuality = results.Count(r => r.Quality == MatchQuality.Poor);

            return new MatchingStatistics
            {
                TotalReferenceRows = totalCount,
                GoodMatches = goodMatches,
                HighQualityMatches = highQuality,
                MediumQualityMatches = mediumQuality,
                LowQualityMatches = lowQuality,
                PoorQualityMatches = poorQuality,
                MatchPercentage = totalCount > 0 ? (double)goodMatches / totalCount * 100 : 0,
                AverageSimilarityScore = results.Any() ? results.Average(r => r.SimilarityScore) : 0
            };
        }

        /// <summary>
        /// Generates line-by-line report without reordering
        /// </summary>
        /// <param name="referenceRows">Reference content rows</param>
        /// <param name="targetRows">Target content rows</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>List of line-by-line match results</returns>
        public async Task<List<LineByLineMatchResult>> GenerateLineByLineReportAsync(
            IEnumerable<ContentRow> referenceRows,
            IEnumerable<ContentRow> targetRows,
            IProgress<string>? progressCallback = null)
        {
            var refList = referenceRows.ToList();
            var targetList = targetRows.ToList();
            var results = new List<LineByLineMatchResult>();

            if (refList.Count != targetList.Count)
            {
                progressCallback?.Report("Warning: Reference and target have different number of rows. Comparing up to minimum length.");
            }

            var minLength = Math.Min(refList.Count, targetList.Count);
            var refWithEmbeddings = refList.Where(r => r.EmbeddingVector != null).ToList();
            var processed = 0;

            for (int i = 0; i < minLength; i++)
            {
                var targetRow = targetList[i];
                var refRow = refList[i];

                if (targetRow.EmbeddingVector == null || refRow.EmbeddingVector == null)
                {
                    results.Add(new LineByLineMatchResult
                    {
                        TargetRow = targetRow,
                        CorrespondingReferenceRow = refRow,
                        LineByLineScore = 0.0,
                        IsGoodLineByLineMatch = false,
                        SuggestedMatch = null
                    });
                    continue;
                }

                var lineScore = GeminiEmbeddingService.CalculateCosineSimilarity(
                    refRow.EmbeddingVector,
                    targetRow.EmbeddingVector);

                bool isGood = lineScore >= _similarityThreshold;

                MatchResult? suggestion = null;
                if (!isGood)
                {
                    // Find best match from all references, without used tracking
                    suggestion = await FindBestMatchAsync(targetRow, refWithEmbeddings, new HashSet<int>());
                }

                results.Add(new LineByLineMatchResult
                {
                    TargetRow = targetRow,
                    CorrespondingReferenceRow = refRow,
                    LineByLineScore = lineScore,
                    IsGoodLineByLineMatch = isGood,
                    SuggestedMatch = suggestion
                });

                processed++;
                if (processed % 10 == 0 || processed == minLength)
                {
                    progressCallback?.Report($"Processed {processed}/{minLength} rows...");
                }
            }

            // Handle extra rows if lengths differ
            if (targetList.Count > refList.Count)
            {
                for (int i = minLength; i < targetList.Count; i++)
                {
                    results.Add(new LineByLineMatchResult
                    {
                        TargetRow = targetList[i],
                        CorrespondingReferenceRow = null,
                        LineByLineScore = 0.0,
                        IsGoodLineByLineMatch = false,
                        SuggestedMatch = await FindBestMatchAsync(targetList[i], refWithEmbeddings, new HashSet<int>())
                    });
                }
            }

            progressCallback?.Report("Line-by-line report generation completed.");

            return results;
        }

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