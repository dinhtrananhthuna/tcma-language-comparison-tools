using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Core.Services;

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
        /// <returns>OperationResult containing list of match results or error info</returns>
        public async Task<OperationResult<List<MatchResult>>> FindMatchesAsync(
            IEnumerable<ContentRow> referenceRows,
            IEnumerable<ContentRow> targetRows,
            IProgress<string>? progressCallback = null)
        {
            try
            {
                var refList = referenceRows?.ToList();
                var targetList = targetRows?.ToList();

                if (refList == null || refList.Count == 0)
                {
                    return OperationResult<List<MatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có dữ liệu reference để so sánh.",
                        TechnicalDetails = "Reference rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo file reference có dữ liệu hợp lệ."
                    });
                }

                if (targetList == null || targetList.Count == 0)
                {
                    return OperationResult<List<MatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có dữ liệu target để so sánh.",
                        TechnicalDetails = "Target rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo file target có dữ liệu hợp lệ."
                    });
                }

                var results = new List<MatchResult>();

                progressCallback?.Report($"Bắt đầu tìm matches cho {refList.Count} reference rows với {targetList.Count} target rows...");

                // Validate that embeddings exist
                var refWithEmbeddings = refList.Where(r => r.EmbeddingVector != null).ToList();
                var targetWithEmbeddings = targetList.Where(t => t.EmbeddingVector != null).ToList();

                if (refWithEmbeddings.Count == 0)
                {
                    return OperationResult<List<MatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Critical,
                        UserMessage = "Không có reference rows nào có embedding vectors.",
                        TechnicalDetails = "No reference rows have embedding vectors",
                        SuggestedAction = "Vui lòng tạo embeddings cho file reference trước."
                    });
                }

                if (targetWithEmbeddings.Count == 0)
                {
                    return OperationResult<List<MatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Critical,
                        UserMessage = "Không có target rows nào có embedding vectors.",
                        TechnicalDetails = "No target rows have embedding vectors",
                        SuggestedAction = "Vui lòng tạo embeddings cho file target trước."
                    });
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

                return OperationResult<List<MatchResult>>.Success(results.OrderBy(r => r.ReferenceRow.OriginalIndex).ToList());
            }
            catch (Exception ex)
            {
                return OperationResult<List<MatchResult>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không mong muốn khi tìm matches.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ.",
                    OriginalException = ex
                });
            }
        }

        /// <summary>
        /// Generates line-by-line report without reordering
        /// </summary>
        /// <param name="referenceRows">Reference content rows</param>
        /// <param name="targetRows">Target content rows</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>OperationResult containing list of line-by-line match results or error info</returns>
        public async Task<OperationResult<List<LineByLineMatchResult>>> GenerateLineByLineReportAsync(
            IEnumerable<ContentRow> referenceRows,
            IEnumerable<ContentRow> targetRows,
            IProgress<string>? progressCallback = null)
        {
            try
            {
                var refList = referenceRows?.ToList();
                var targetList = targetRows?.ToList();

                if (refList == null || refList.Count == 0)
                {
                    return OperationResult<List<LineByLineMatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có dữ liệu reference để so sánh.",
                        TechnicalDetails = "Reference rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo file reference có dữ liệu hợp lệ."
                    });
                }

                if (targetList == null || targetList.Count == 0)
                {
                    return OperationResult<List<LineByLineMatchResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có dữ liệu target để so sánh.",
                        TechnicalDetails = "Target rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo file target có dữ liệu hợp lệ."
                    });
                }

                var results = new List<LineByLineMatchResult>();

                if (refList.Count != targetList.Count)
                {
                    progressCallback?.Report("Warning: Reference và target có số dòng khác nhau. So sánh theo số dòng ít hơn.");
                }

                var minLength = Math.Min(refList.Count, targetList.Count);
                var refWithEmbeddings = refList.Where(r => r.EmbeddingVector != null).ToList();
                var processed = 0;

                progressCallback?.Report($"Bắt đầu so sánh line-by-line cho {minLength} dòng...");

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
                        progressCallback?.Report($"Đã xử lý {processed}/{minLength} dòng...");
                    }
                }

                // Handle extra rows if lengths differ
                if (targetList.Count > refList.Count)
                {
                    for (int i = minLength; i < targetList.Count; i++)
                    {
                        var suggestion = await FindBestMatchAsync(targetList[i], refWithEmbeddings, new HashSet<int>());
                        results.Add(new LineByLineMatchResult
                        {
                            TargetRow = targetList[i],
                            CorrespondingReferenceRow = null,
                            LineByLineScore = 0.0,
                            IsGoodLineByLineMatch = false,
                            SuggestedMatch = suggestion
                        });
                    }
                }

                progressCallback?.Report("Hoàn thành so sánh line-by-line.");

                return OperationResult<List<LineByLineMatchResult>>.Success(results);
            }
            catch (Exception ex)
            {
                return OperationResult<List<LineByLineMatchResult>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không mong muốn khi tạo báo cáo line-by-line.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ.",
                    OriginalException = ex
                });
            }
        }

        /// <summary>
        /// Tạo danh sách target đã align với reference (có dòng trống cho dòng thiếu)
        /// Sử dụng optimal bipartite matching thay vì greedy để đảm bảo alignment tối ưu
        /// </summary>
        public async Task<AlignedTargetResult> GenerateAlignedTargetFileAsync(
            IEnumerable<ContentRow> referenceRows,
            IEnumerable<ContentRow> targetRows,
            IProgress<string>? progressCallback = null)
        {
            var refList = referenceRows?.ToList() ?? new List<ContentRow>();
            var targetList = targetRows?.ToList() ?? new List<ContentRow>();
            var alignedRows = new List<AlignedTargetRow>();

            // Lấy các dòng có embedding
            var refWithEmbeddings = refList.Where(r => r.EmbeddingVector != null).ToList();
            var targetWithEmbeddings = targetList.Where(t => t.EmbeddingVector != null).ToList();

            // Tạo similarity matrix
            var similarityMatrix = new double[refWithEmbeddings.Count, targetWithEmbeddings.Count];
            for (int i = 0; i < refWithEmbeddings.Count; i++)
            {
                for (int j = 0; j < targetWithEmbeddings.Count; j++)
                {
                    similarityMatrix[i, j] = GeminiEmbeddingService.CalculateCosineSimilarity(
                        refWithEmbeddings[i].EmbeddingVector!, 
                        targetWithEmbeddings[j].EmbeddingVector!);
                }
            }

            // Tìm optimal matching bằng Hungarian-like greedy approach (simplified)
            // Tạo map từ ref index → target match
            var refToTargetMap = new Dictionary<int, (ContentRow target, double score)>();
            var usedTargetIndexes = new HashSet<int>();

            // Sắp xếp theo thứ tự similarity score giảm dần để ưu tiên match tốt nhất
            var allMatches = new List<(int refIdx, int targetIdx, double score)>();
            for (int i = 0; i < refWithEmbeddings.Count; i++)
            {
                for (int j = 0; j < targetWithEmbeddings.Count; j++)
                {
                    if (similarityMatrix[i, j] >= _similarityThreshold)
                    {
                        allMatches.Add((i, j, similarityMatrix[i, j]));
                    }
                }
            }

            // Sort by score descending để ưu tiên match tốt nhất trước
            allMatches.Sort((a, b) => b.score.CompareTo(a.score));

            // Greedy assignment: chọn match tốt nhất mà không bị conflict
            foreach (var (refIdx, targetIdx, score) in allMatches)
            {
                if (!refToTargetMap.ContainsKey(refIdx) && !usedTargetIndexes.Contains(targetIdx))
                {
                    refToTargetMap[refIdx] = (targetWithEmbeddings[targetIdx], score);
                    usedTargetIndexes.Add(targetIdx);
                }
            }

            // Tạo aligned rows theo thứ tự reference
            for (int i = 0; i < refList.Count; i++)
            {
                var refRow = refList[i];
                
                // Tìm index của ref row này trong danh sách có embedding bằng OriginalIndex
                var refEmbeddingIdx = refWithEmbeddings.FindIndex(r => r.OriginalIndex == refRow.OriginalIndex);
                
                if (refEmbeddingIdx >= 0 && refToTargetMap.ContainsKey(refEmbeddingIdx))
                {
                    var (targetRow, score) = refToTargetMap[refEmbeddingIdx];
                    alignedRows.Add(new AlignedTargetRow
                    {
                        ReferenceIndex = i,
                        TargetRow = targetRow,
                        SimilarityScore = score
                    });
                }
                else
                {
                    // Không có match hoặc không có embedding → dòng trống
                    alignedRows.Add(new AlignedTargetRow
                    {
                        ReferenceIndex = i,
                        TargetRow = null,
                        SimilarityScore = null
                    });
                }
            }

            // Các dòng target không được dùng
            var usedTargetOriginalIndexes = refToTargetMap.Values.Select(v => v.target.OriginalIndex).ToHashSet();
            var unusedTargetRows = targetList.Where(t => !usedTargetOriginalIndexes.Contains(t.OriginalIndex)).ToList();

            return new AlignedTargetResult
            {
                AlignedRows = alignedRows,
                UnusedTargetRows = unusedTargetRows,
                TotalReferenceRows = refList.Count,
                MatchedRows = alignedRows.Count(r => r.HasMatch),
                MissingRows = alignedRows.Count(r => !r.HasMatch),
                UnusedRows = unusedTargetRows.Count
            };
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
        /// Tạo dữ liệu aligned để hiển thị trong DataGrid (sử dụng chung thuật toán với export)
        /// Bao gồm cả những dòng target không match ở cuối danh sách
        /// </summary>
        /// <param name="referenceRows">Danh sách reference rows đã có embedding</param>
        /// <param name="targetRows">Danh sách target rows đã có embedding (đã dịch)</param>
        /// <param name="originalTargetRows">Danh sách target rows gốc (chưa dịch)</param>
        /// <param name="translationResults">Kết quả dịch từ translation service</param>
        /// <param name="progressCallback">Optional progress callback</param>
        /// <returns>Danh sách AlignedDisplayRow theo thứ tự reference, sau đó là unmatched target rows</returns>
        public async Task<List<AlignedDisplayRow>> GenerateAlignedDisplayDataAsync(
            List<ContentRow> referenceRows, 
            List<ContentRow> targetRows,
            List<ContentRow>? originalTargetRows = null,
            List<TranslationResult>? translationResults = null,
            IProgress<string>? progressCallback = null)
        {
            // Debug logging
            Console.WriteLine($"🔍 [GenerateAlignedDisplayDataAsync] Debug Info:");
            Console.WriteLine($"   - referenceRows.Count: {referenceRows.Count}");
            Console.WriteLine($"   - targetRows.Count: {targetRows.Count}");
            Console.WriteLine($"   - originalTargetRows: {(originalTargetRows == null ? "NULL" : $"{originalTargetRows.Count} items")}");
            Console.WriteLine($"   - translationResults: {(translationResults == null ? "NULL" : $"{translationResults.Count} items")}");
            
            if (originalTargetRows != null && originalTargetRows.Count > 0)
            {
                Console.WriteLine($"   - Sample originalTargetRows[0]: ContentId='{originalTargetRows[0].ContentId}', Content='{originalTargetRows[0].Content[..Math.Min(50, originalTargetRows[0].Content.Length)]}...'");
            }
            
            if (translationResults != null && translationResults.Count > 0)
            {
                Console.WriteLine($"   - Sample translationResults[0]: ContentId='{translationResults[0].ContentId}', TranslatedContent='{translationResults[0].TranslatedContent[..Math.Min(50, translationResults[0].TranslatedContent.Length)]}...'");
            }
            
            // Sử dụng lại logic từ GenerateAlignedTargetFileAsync
            var alignedResult = await GenerateAlignedTargetFileAsync(referenceRows, targetRows, progressCallback);
            
            var displayRows = new List<AlignedDisplayRow>();
            
            // Tạo dictionary để lookup nội dung gốc và dịch
            var originalContentMap = originalTargetRows?.ToDictionary(r => r.ContentId, r => r.Content) ?? new Dictionary<string, string>();
            var translationMap = translationResults?.ToDictionary(t => t.ContentId, t => t.TranslatedContent) ?? new Dictionary<string, string>();
            
            Console.WriteLine($"   - originalContentMap.Count: {originalContentMap.Count}");
            Console.WriteLine($"   - translationMap.Count: {translationMap.Count}");
            
            // Thêm các dòng aligned theo thứ tự reference
            for (int i = 0; i < alignedResult.AlignedRows.Count; i++)
            {
                var alignedRow = alignedResult.AlignedRows[i];
                var referenceRow = referenceRows[alignedRow.ReferenceIndex];
                
                string originalContent = string.Empty;
                string translatedContent = string.Empty;
                
                if (alignedRow.TargetRow != null)
                {
                    var contentId = alignedRow.TargetRow.ContentId;
                    originalContent = originalContentMap.GetValueOrDefault(contentId, alignedRow.TargetRow.Content);
                    translatedContent = translationMap.GetValueOrDefault(contentId, string.Empty);
                    
                    // Debug cho item đầu tiên
                    if (i == 0)
                    {
                        Console.WriteLine($"   - First item mapping: ContentId='{contentId}'");
                        Console.WriteLine($"     * alignedRow.TargetRow.Content: '{alignedRow.TargetRow.Content[..Math.Min(50, alignedRow.TargetRow.Content.Length)]}...'");
                        Console.WriteLine($"     * originalContent: '{originalContent[..Math.Min(50, originalContent.Length)]}...'");
                        Console.WriteLine($"     * translatedContent: '{translatedContent[..Math.Min(50, translatedContent.Length)]}...'");
                    }
                }
                
                // Tạo AlignedDisplayRow với nội dung gốc (TargetContent) và bản dịch (TranslatedContent)
                var displayRow = new AlignedDisplayRow
                {
                    RowType = AlignedRowType.ReferenceAligned,
                    RefLineNumber = alignedRow.ReferenceIndex + 1,
                    RefContent = referenceRow.Content,
                    TargetLineNumber = alignedRow.TargetRow?.OriginalIndex + 1,
                    TargetContent = originalContent, // Nội dung gốc (tiếng Trung)
                    TranslatedContent = translatedContent, // Bản dịch (tiếng Anh)
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
                displayRows.Add(displayRow);
            }
            
            // Thêm các dòng target không match ở cuối danh sách (nếu có)
            if (alignedResult.UnusedTargetRows.Any())
            {
                progressCallback?.Report($"Thêm {alignedResult.UnusedTargetRows.Count} dòng target không match vào cuối danh sách...");
                
                foreach (var unmatchedTargetRow in alignedResult.UnusedTargetRows.OrderBy(t => t.OriginalIndex))
                {
                    var contentId = unmatchedTargetRow.ContentId;
                    var originalContent = originalContentMap.GetValueOrDefault(contentId, unmatchedTargetRow.Content);
                    var translatedContent = translationMap.GetValueOrDefault(contentId, string.Empty);
                    
                    var unmatchedDisplayRow = new AlignedDisplayRow
                    {
                        RowType = AlignedRowType.UnmatchedTarget,
                        RefLineNumber = null,
                        RefContent = string.Empty,
                        TargetLineNumber = unmatchedTargetRow.OriginalIndex + 1,
                        TargetContent = originalContent, // Nội dung gốc (tiếng Trung)
                        TranslatedContent = translatedContent, // Bản dịch (tiếng Anh)
                        TargetContentId = unmatchedTargetRow.ContentId,
                        Status = "Unmatched Target",
                        SimilarityScore = null,
                        Quality = MatchQuality.Poor
                    };
                    displayRows.Add(unmatchedDisplayRow);
                }
            }
            
            Console.WriteLine($"   - Generated {displayRows.Count} display rows");
            if (displayRows.Count > 0)
            {
                var firstRow = displayRows[0];
                Console.WriteLine($"   - First row: TargetContent='{firstRow.TargetContent[..Math.Min(50, firstRow.TargetContent.Length)]}...', TranslatedContent='{firstRow.TranslatedContent[..Math.Min(50, firstRow.TranslatedContent.Length)]}...'");
            }
            
            return displayRows;
        }
    }

} 