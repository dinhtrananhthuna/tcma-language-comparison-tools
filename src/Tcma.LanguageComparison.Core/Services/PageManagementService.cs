using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for managing multi-page processing workflow
    /// </summary>
    public class PageManagementService : IDisposable
    {
        private readonly Dictionary<string, PageMatchingResult> _cachedResults = new();
        private readonly ZipExtractionService _zipExtractor;
        private readonly FileMatchingService _fileMatcher;
        private readonly ContentMatchingService _contentMatcher;
        private readonly CsvReaderService _csvReader;
        private readonly TextPreprocessingService _textPreprocessor;
        private readonly GeminiEmbeddingService _embeddingService;
        private readonly IGeminiTranslationService _translationService;
        private bool _disposed = false;

        public PageManagementService(
            double similarityThreshold = 0.35,
            string? apiKey = null,
            IGeminiTranslationService? translationService = null)
        {
            _zipExtractor = new ZipExtractionService();
            _fileMatcher = new FileMatchingService();
            _contentMatcher = new ContentMatchingService(similarityThreshold);
            _csvReader = new CsvReaderService();
            _textPreprocessor = new TextPreprocessingService();
            _translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _embeddingService = new GeminiEmbeddingService(apiKey);
            }
            else
            {
                // Will be initialized later when API key is provided
                _embeddingService = null!;
            }
        }

        /// <summary>
        /// Initializes or updates the embedding service with a new API key
        /// </summary>
        /// <param name="apiKey">Google Gemini API key</param>
        public void InitializeEmbeddingService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            // Create new service with updated API key
            // Note: We can't directly modify the readonly field, so we need to work around this
            // For now, we'll create a new instance if needed
            if (_embeddingService == null)
            {
                // Use reflection to set the readonly field (not ideal but necessary for this design)
                var field = typeof(PageManagementService).GetField("_embeddingService", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, new GeminiEmbeddingService(apiKey));
            }
        }

        /// <summary>
        /// Loads pages from two ZIP archives containing reference and target files
        /// </summary>
        /// <param name="referenceZipPath">Path to reference ZIP file</param>
        /// <param name="targetZipPath">Path to target ZIP file</param>
        /// <returns>OperationResult containing file extraction result</returns>
        public async Task<OperationResult<FileExtractionResult>> LoadPagesFromZipsAsync(
            string referenceZipPath, 
            string targetZipPath)
        {
            try
            {
                // Clear any existing cached results
                ClearCache();

                // Extract files from both ZIP archives
                var refExtractionResult = await _zipExtractor.ExtractCsvFilesAsync(referenceZipPath);
                if (!refExtractionResult.IsSuccess)
                {
                    return OperationResult<FileExtractionResult>.Failure(refExtractionResult.Error!);
                }

                var targetExtractionResult = await _zipExtractor.ExtractCsvFilesAsync(targetZipPath);
                if (!targetExtractionResult.IsSuccess)
                {
                    return OperationResult<FileExtractionResult>.Failure(targetExtractionResult.Error!);
                }

                var refFiles = refExtractionResult.Data!;
                var targetFiles = targetExtractionResult.Data!;

                // Pair the extracted files
                var pairingResult = await _fileMatcher.PairFilesAsync(refFiles, targetFiles);
                if (!pairingResult.IsSuccess)
                {
                    return OperationResult<FileExtractionResult>.Failure(pairingResult.Error!);
                }

                var extractionResult = pairingResult.Data!;
                extractionResult.ExtractionDirectory = _zipExtractor.ExtractionDirectory;

                return OperationResult<FileExtractionResult>.Success(extractionResult);
            }
            catch (Exception ex)
            {
                return OperationResult<FileExtractionResult>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không xác định khi tải pages từ ZIP files.",
                    TechnicalDetails = $"Unexpected error during ZIP processing: {ex.Message}",
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ."
                });
            }
        }

        /// <summary>
        /// Processes a single page and returns the matching results
        /// </summary>
        /// <param name="pageInfo">Information about the page to process</param>
        /// <param name="progress">Progress reporter for UI updates</param>
        /// <returns>OperationResult containing page matching results</returns>
        public async Task<OperationResult<PageMatchingResult>> ProcessPageAsync(
            PageInfo pageInfo, 
            IProgress<string>? progress = null)
        {
            if (_embeddingService == null)
            {
                return OperationResult<PageMatchingResult>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.Configuration,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Gemini API key chưa được cấu hình.",
                    TechnicalDetails = "Embedding service not initialized",
                    SuggestedAction = "Vui lòng thiết lập API key trong Settings."
                });
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Update page status
                pageInfo.Status = PageStatus.Processing;
                pageInfo.Progress = 0;

                progress?.Report($"Bắt đầu xử lý page: {pageInfo.PageName}");

                // Validate file paths
                if (!File.Exists(pageInfo.ReferenceFilePath))
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = "Reference file không tồn tại.";
                    return OperationResult<PageMatchingResult>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.FileAccess,
                        Severity = ErrorSeverity.High,
                        UserMessage = $"Reference file không tồn tại: {pageInfo.ReferenceFilePath}",
                        SuggestedAction = "Vui lòng kiểm tra file path."
                    });
                }

                if (!File.Exists(pageInfo.TargetFilePath))
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = "Target file không tồn tại.";
                    return OperationResult<PageMatchingResult>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.FileAccess,
                        Severity = ErrorSeverity.High,
                        UserMessage = $"Target file không tồn tại: {pageInfo.TargetFilePath}",
                        SuggestedAction = "Vui lòng kiểm tra file path."
                    });
                }

                pageInfo.Progress = 10;
                progress?.Report("Đọc CSV files...");

                // Load CSV files
                var refResult = await _csvReader.ReadContentRowsAsync(pageInfo.ReferenceFilePath);
                if (!refResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi đọc reference file: {refResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(refResult.Error!);
                }

                var targetResult = await _csvReader.ReadContentRowsAsync(pageInfo.TargetFilePath);
                if (!targetResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi đọc target file: {targetResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(targetResult.Error!);
                }

                var referenceRows = refResult.Data!;
                var targetRows = targetResult.Data!;

                pageInfo.Progress = 20;
                progress?.Report($"Đã tải {referenceRows.Count} reference và {targetRows.Count} target rows");

                // Dịch toàn bộ targetRows sang tiếng Anh
                progress?.Report("Đang dịch nội dung target sang tiếng Anh bằng Gemini Flash...");
                var translateResult = await _translationService.TranslateBatchAsync(targetRows, "auto", "en");
                if (!translateResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi dịch target: {translateResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(translateResult.Error!);
                }
                var translated = translateResult.Data!;
                // Gán lại content đã dịch vào targetRows (tạo ContentRow mới do Content là init-only)
                var translatedDict = translated.ToDictionary(t => t.ContentId, t => t.TranslatedContent);
                for (int i = 0; i < targetRows.Count; i++)
                {
                    var row = targetRows[i];
                    if (translatedDict.TryGetValue(row.ContentId, out var trans))
                    {
                        targetRows[i] = row with { Content = trans };
                    }
                }

                progress?.Report("Đã dịch xong target, bắt đầu xử lý nội dung...");

                // Preprocess content
                progress?.Report("Xử lý nội dung...");
                _textPreprocessor.ProcessContentRows(referenceRows);
                _textPreprocessor.ProcessContentRows(targetRows);

                pageInfo.Progress = 30;

                // Test API connection first
                progress?.Report("Kiểm tra kết nối API...");
                var connectionResult = await _embeddingService.TestConnectionAsync();
                if (!connectionResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"API connection failed: {connectionResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(connectionResult.Error!);
                }

                pageInfo.Progress = 40;

                // Generate embeddings for reference content
                progress?.Report("Tạo embeddings cho reference content...");
                var refEmbeddingResult = await _embeddingService.GenerateEmbeddingsAsync(referenceRows, progress);
                if (!refEmbeddingResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi tạo embeddings cho reference: {refEmbeddingResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(refEmbeddingResult.Error!);
                }

                pageInfo.Progress = 60;

                // Generate embeddings for target content
                progress?.Report("Tạo embeddings cho target content...");
                var targetEmbeddingResult = await _embeddingService.GenerateEmbeddingsAsync(targetRows, progress);
                if (!targetEmbeddingResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi tạo embeddings cho target: {targetEmbeddingResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(targetEmbeddingResult.Error!);
                }

                pageInfo.Progress = 80;

                // Perform line-by-line matching
                progress?.Report("Thực hiện matching...");
                var matchingResult = await _contentMatcher.GenerateLineByLineReportAsync(
                    referenceRows, targetRows, progress);
                
                if (!matchingResult.IsSuccess)
                {
                    pageInfo.Status = PageStatus.Error;
                    pageInfo.ErrorMessage = $"Lỗi trong quá trình matching: {matchingResult.Error?.UserMessage}";
                    return OperationResult<PageMatchingResult>.Failure(matchingResult.Error!);
                }

                var lineByLineResults = matchingResult.Data!;

                pageInfo.Progress = 90;

                // Calculate statistics
                var matchResults = lineByLineResults.Select(r => new MatchResult
                {
                    ReferenceRow = r.CorrespondingReferenceRow ?? new ContentRow(),
                    MatchedRow = r.TargetRow,
                    SimilarityScore = r.LineByLineScore,
                    IsGoodMatch = r.IsGoodLineByLineMatch
                }).ToList();

                var stats = _contentMatcher.GetMatchingStatistics(matchResults);

                stopwatch.Stop();

                // Create result
                var pageResult = new PageMatchingResult
                {
                    PageInfo = pageInfo,
                    Results = lineByLineResults,
                    ProcessedAt = DateTime.Now,
                    Statistics = new PageStatistics
                    {
                        TotalReferenceRows = stats.TotalReferenceRows,
                        TotalTargetRows = targetRows.Count, // Get directly from original data
                        GoodMatches = stats.GoodMatches,
                        AverageSimilarityScore = stats.AverageSimilarityScore,
                        ProcessingDuration = stopwatch.Elapsed
                    }
                };

                // Update page status
                pageInfo.Status = PageStatus.Completed;
                pageInfo.LastProcessed = DateTime.Now;
                pageInfo.Progress = 100;
                pageInfo.ErrorMessage = null;

                // Cache the result
                _cachedResults[pageInfo.PageName] = pageResult;
                pageInfo.IsResultsCached = true;

                progress?.Report($"Hoàn thành xử lý page {pageInfo.PageName} trong {stopwatch.Elapsed.TotalSeconds:F1}s");

                return OperationResult<PageMatchingResult>.Success(pageResult);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                pageInfo.Status = PageStatus.Error;
                pageInfo.ErrorMessage = $"Unexpected error: {ex.Message}";

                return OperationResult<PageMatchingResult>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không xác định khi xử lý page.",
                    TechnicalDetails = $"Unexpected error processing page {pageInfo.PageName}: {ex.Message}",
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ."
                });
            }
        }

        /// <summary>
        /// Gets cached results for a page if available
        /// </summary>
        /// <param name="pageName">Name of the page</param>
        /// <returns>Cached results or null if not found</returns>
        public PageMatchingResult? GetCachedResult(string pageName)
        {
            if (string.IsNullOrWhiteSpace(pageName))
                return null;

            return _cachedResults.ContainsKey(pageName) ? _cachedResults[pageName] : null;
        }

        /// <summary>
        /// Checks if results are cached for a page
        /// </summary>
        /// <param name="pageName">Name of the page</param>
        /// <returns>True if results are cached</returns>
        public bool HasCachedResult(string pageName)
        {
            return !string.IsNullOrWhiteSpace(pageName) && _cachedResults.ContainsKey(pageName);
        }

        /// <summary>
        /// Clears all cached results
        /// </summary>
        public void ClearCache()
        {
            _cachedResults.Clear();
        }

        /// <summary>
        /// Removes cached result for a specific page
        /// </summary>
        /// <param name="pageName">Name of the page</param>
        public void RemoveCachedResult(string pageName)
        {
            if (!string.IsNullOrWhiteSpace(pageName))
            {
                _cachedResults.Remove(pageName);
            }
        }

        /// <summary>
        /// Gets summary of all cached results
        /// </summary>
        /// <returns>Dictionary of page names and their processing summaries</returns>
        public Dictionary<string, string> GetCachedResultsSummary()
        {
            return _cachedResults.ToDictionary(
                kvp => kvp.Key,
                kvp => $"Processed: {kvp.Value.ProcessedAt:HH:mm:ss}, " +
                       $"Matches: {kvp.Value.Statistics.GoodMatches}/{kvp.Value.Statistics.TotalReferenceRows}, " +
                       $"Avg Score: {kvp.Value.Statistics.AverageSimilarityScore:F3}"
            );
        }

        /// <summary>
        /// Updates page status (for UI synchronization)
        /// </summary>
        /// <param name="pageInfo">Page to update</param>
        public void UpdatePageStatus(PageInfo pageInfo)
        {
            if (pageInfo == null) return;

            // Update cached status if result exists
            if (HasCachedResult(pageInfo.PageName))
            {
                pageInfo.Status = PageStatus.Cached;
                pageInfo.IsResultsCached = true;
            }
        }

        /// <summary>
        /// Gets the extraction directory path
        /// </summary>
        public string ExtractionDirectory => _zipExtractor.ExtractionDirectory;

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _zipExtractor?.Dispose();
                    ClearCache();
                }

                _disposed = true;
            }
        }

        ~PageManagementService()
        {
            Dispose(false);
        }
    }
} 