using Mscc.GenerativeAI;
using Tcma.LanguageComparison.Core.Models;
using System.Numerics;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for generating embeddings using Google Gemini API
    /// </summary>
    public class GeminiEmbeddingService
    {
        private readonly GoogleAI _googleAI;
        private readonly GenerativeModel _model;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxEmbeddingBatchSize;
        private int _currentBatchSize;
        private DateTime _lastBatchTime;
        private double _averageResponseTime;
        private const int MaxConcurrentRequests = 5; // Limit concurrent API calls

        /// <summary>
        /// Initializes the Gemini embedding service
        /// </summary>
        /// <param name="apiKey">Google AI API key</param>
        /// <param name="maxEmbeddingBatchSize">Maximum batch size for embedding requests</param>
        public GeminiEmbeddingService(string apiKey, int maxEmbeddingBatchSize = 50)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            _googleAI = new GoogleAI(apiKey);
            _model = _googleAI.GenerativeModel(Model.TextEmbedding004);
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
            _maxEmbeddingBatchSize = maxEmbeddingBatchSize;
            _currentBatchSize = Math.Max(5, maxEmbeddingBatchSize / 4); // Start with 25% of max
            _lastBatchTime = DateTime.Now;
            _averageResponseTime = 1000.0; // Start with 1 second estimate
        }

        /// <summary>
        /// Adapts batch size based on API performance and error rates
        /// </summary>
        private void AdaptBatchSize(bool batchSuccessful, double batchDurationMs, int errorsInBatch, int batchSize)
        {
            // Update average response time with exponential moving average
            _averageResponseTime = 0.7 * _averageResponseTime + 0.3 * (batchDurationMs / batchSize);

            // Calculate success rate for this batch
            double successRate = (double)(batchSize - errorsInBatch) / batchSize;

            // Adapt batch size based on performance
            if (successRate > 0.9 && batchDurationMs < 3000) // High success, fast response
            {
                _currentBatchSize = Math.Min(_maxEmbeddingBatchSize, (int)(_currentBatchSize * 1.2));
            }
            else if (successRate < 0.7 || batchDurationMs > 10000) // Many errors or slow response
            {
                _currentBatchSize = Math.Max(5, (int)(_currentBatchSize * 0.7));
            }
            else if (batchDurationMs > 5000) // Moderate slowness
            {
                _currentBatchSize = Math.Max(5, (int)(_currentBatchSize * 0.9));
            }
        }

        /// <summary>
        /// Gets optimal delay between batches based on current performance
        /// </summary>
        private int GetAdaptiveDelay()
        {
            // Shorter delays for smaller batches, longer for larger ones
            int baseDelay = _averageResponseTime > 2000 ? 800 : 300;
            double batchFactor = (double)_currentBatchSize / _maxEmbeddingBatchSize;
            return (int)(baseDelay * (0.5 + 0.5 * batchFactor));
        }

        /// <summary>
        /// Generates embedding vector for a single text with retry logic
        /// </summary>
        /// <param name="text">Text content to generate embedding for</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <returns>OperationResult containing embedding vector or error info</returns>
        public async Task<OperationResult<float[]>> GetEmbeddingAsync(string text, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return OperationResult<float[]>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.Medium,
                    UserMessage = "Nội dung text rỗng, không thể tạo embedding.",
                    TechnicalDetails = "Text parameter is null or empty",
                    SuggestedAction = "Vui lòng đảm bảo nội dung không rỗng."
                });
            }

            // Check content length
            if (text.Length > 8000)
            {
                return OperationResult<float[]>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.Medium,
                    UserMessage = "Nội dung quá dài để tạo embedding.",
                    TechnicalDetails = $"Text length: {text.Length}, max allowed: 8000",
                    SuggestedAction = "Vui lòng rút gọn nội dung hoặc chia nhỏ text."
                });
            }

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                await _semaphore.WaitAsync();
                try
                {
                    var response = await _model.EmbedContent(text);
                    var values = response.Embedding?.Values;
                    
                    if (values == null || !values.Any())
                    {
                        return OperationResult<float[]>.Failure(new ErrorInfo
                        {
                            Category = ErrorCategory.ApiProcessing,
                            Severity = ErrorSeverity.High,
                            UserMessage = "API không trả về embedding vector.",
                            TechnicalDetails = $"Empty embedding response for text: {text[..Math.Min(50, text.Length)]}...",
                            SuggestedAction = "Vui lòng thử lại hoặc kiểm tra API key."
                        });
                    }

                    return OperationResult<float[]>.Success(values.ToArray());
                }
                catch (HttpRequestException ex)
                {
                    var error = AnalyzeHttpException(ex, text);
                    
                    // Check if this is a retriable error
                    if (IsRetriableError(error) && attempt < maxRetries)
                    {
                        var delay = CalculateRetryDelay(attempt);
                        await Task.Delay(delay);
                        continue;
                    }
                    
                    return OperationResult<float[]>.Failure(error);
                }
                catch (TaskCanceledException ex)
                {
                    var error = new ErrorInfo
                    {
                        Category = ErrorCategory.NetworkConnection,
                        Severity = ErrorSeverity.Medium,
                        UserMessage = "Timeout khi gọi API Gemini.",
                        TechnicalDetails = ex.Message,
                        SuggestedAction = "Vui lòng kiểm tra kết nối mạng và thử lại.",
                        OriginalException = ex,
                        ContextInfo = $"Text length: {text.Length}"
                    };

                    // Timeout is retriable
                    if (attempt < maxRetries)
                    {
                        var delay = CalculateRetryDelay(attempt);
                        await Task.Delay(delay);
                        continue;
                    }
                    
                    return OperationResult<float[]>.Failure(error);
                }
                catch (ArgumentException ex) when (ex.ParamName == "apiKey")
                {
                    return OperationResult<float[]>.Failure(CommonErrors.InvalidApiKey());
                }
                catch (Exception ex)
                {
                    var error = new ErrorInfo
                    {
                        Category = ErrorCategory.ApiProcessing,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Lỗi không xác định khi gọi API Gemini.",
                        TechnicalDetails = ex.Message,
                        SuggestedAction = "Vui lòng thử lại hoặc liên hệ support.",
                        OriginalException = ex,
                        ContextInfo = $"Text: {text[..Math.Min(100, text.Length)]}"
                    };

                    // Only retry if it's a transient error
                    if (IsTransientError(ex) && attempt < maxRetries)
                    {
                        var delay = CalculateRetryDelay(attempt);
                        await Task.Delay(delay);
                        continue;
                    }
                    
                    return OperationResult<float[]>.Failure(error);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            // Should never reach here, but safety net
            return OperationResult<float[]>.Failure(CommonErrors.UnexpectedError(new Exception("Retry loop completed without result")));
        }

        /// <summary>
        /// Generates embeddings for multiple content rows with improved error handling and parallel processing
        /// </summary>
        /// <param name="contentRows">Content rows to process</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <param name="maxRetries">Maximum retry attempts per item</param>
        /// <returns>OperationResult with processing statistics</returns>
        public async Task<OperationResult<EmbeddingProcessingStats>> GenerateEmbeddingsAsync(
            IEnumerable<ContentRow> contentRows, 
            IProgress<string>? progressCallback = null,
            int maxRetries = 3)
        {
            var rows = contentRows?.ToList();
            if (rows == null || rows.Count == 0)
            {
                return OperationResult<EmbeddingProcessingStats>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.Medium,
                    UserMessage = "Không có dữ liệu để tạo embeddings.",
                    TechnicalDetails = "Content rows collection is null or empty",
                    SuggestedAction = "Vui lòng đảm bảo có dữ liệu trước khi tạo embeddings."
                });
            }

            var validRows = rows.Where(row => !string.IsNullOrWhiteSpace(row.CleanContent)).ToList();
            if (validRows.Count == 0)
            {
                return OperationResult<EmbeddingProcessingStats>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Không có nội dung hợp lệ để tạo embeddings.",
                    TechnicalDetails = "All content rows have empty CleanContent",
                    SuggestedAction = "Vui lòng kiểm tra và preprocess dữ liệu trước."
                });
            }

            var total = validRows.Count;
            var processed = 0;
            var succeeded = 0;
            var failed = 0;
            var errors = new List<string>();
            var retryAttempts = 0;

            progressCallback?.Report($"Bắt đầu tạo embeddings cho {total} nội dung...");

            // Process in adaptive batches to optimize API performance
            int remainingRows = validRows.Count;
            int startIndex = 0;
            
            progressCallback?.Report($"Starting with adaptive batch size: {_currentBatchSize} (max: {_maxEmbeddingBatchSize})");

            while (startIndex < validRows.Count)
            {
                // Determine current batch size (may be smaller for last batch)
                int currentBatchSize = Math.Min(_currentBatchSize, remainingRows);
                var batch = validRows.Skip(startIndex).Take(currentBatchSize).ToArray();
                
                var batchStartTime = DateTime.Now;
                int batchErrors = 0;

                var batchTasks = batch.Select(async row =>
                {
                    var result = await GetEmbeddingAsync(row.CleanContent, maxRetries);
                    var currentProgress = Interlocked.Increment(ref processed);

                    if (result.IsSuccess)
                    {
                        row.EmbeddingVector = result.Data;
                        Interlocked.Increment(ref succeeded);
                    }
                    else
                    {
                        Interlocked.Increment(ref failed);
                        Interlocked.Increment(ref batchErrors);
                        var errorMsg = $"ContentId {row.ContentId}: {result.Error?.UserMessage}";
                        lock (errors) { errors.Add(errorMsg); }
                        
                        // Count retry attempts from error context
                        if (result.Error?.TechnicalDetails.Contains("retry", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            Interlocked.Increment(ref retryAttempts);
                        }
                    }

                    var reportInterval = Math.Max(1, _maxEmbeddingBatchSize / 5); // Report 5 times per batch
                    if (currentProgress % reportInterval == 0 || currentProgress == total)
                    {
                        progressCallback?.Report($"Đã xử lý {currentProgress}/{total} nội dung (Thành công: {succeeded}, Lỗi: {failed}) [BatchSize: {_currentBatchSize}]");
                    }
                }).ToArray();

                await Task.WhenAll(batchTasks);

                // Measure batch performance and adapt
                var batchDuration = (DateTime.Now - batchStartTime).TotalMilliseconds;
                AdaptBatchSize(batchErrors == 0, batchDuration, batchErrors, currentBatchSize);

                startIndex += currentBatchSize;
                remainingRows -= currentBatchSize;

                // Add adaptive delay between batches
                if (remainingRows > 0)
                {
                    var delay = GetAdaptiveDelay();
                    await Task.Delay(delay);
                }
            }

            var stats = new EmbeddingProcessingStats
            {
                TotalRows = total,
                SuccessfulRows = succeeded,
                FailedRows = failed,
                ErrorMessages = errors,
                SuccessRate = total > 0 ? (double)succeeded / total * 100 : 0,
                RetryAttempts = retryAttempts
            };

            progressCallback?.Report($"Hoàn thành! Đã tạo embeddings cho {succeeded}/{total} nội dung (Tỷ lệ thành công: {stats.SuccessRate:F1}%, Retries: {retryAttempts}).");

            if (failed > 0 && succeeded == 0)
            {
                return OperationResult<EmbeddingProcessingStats>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.ApiProcessing,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Không thể tạo embedding nào.",
                    TechnicalDetails = $"All {total} requests failed",
                    SuggestedAction = "Vui lòng kiểm tra API key và kết nối mạng."
                });
            }

            return OperationResult<EmbeddingProcessingStats>.Success(stats);
        }

        /// <summary>
        /// Tests the API connection with retry logic
        /// </summary>
        /// <returns>OperationResult indicating connection status</returns>
        public async Task<OperationResult<bool>> TestConnectionAsync()
        {
            return await GetEmbeddingAsync("Test connection", maxRetries: 2)
                .ContinueWith(task =>
                {
                    var result = task.Result;
                    return result.IsSuccess 
                        ? OperationResult<bool>.Success(true)
                        : OperationResult<bool>.Failure(result.Error!);
                });
        }

        /// <summary>
        /// Determines if an error is retriable based on error type and HTTP status
        /// </summary>
        private static bool IsRetriableError(ErrorInfo error)
        {
            return error.Category switch
            {
                ErrorCategory.NetworkConnection => true,
                ErrorCategory.ApiRateLimit => true,
                ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("503") => true, // Service unavailable
                ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("502") => true, // Bad gateway
                ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("504") => true, // Gateway timeout
                ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if an exception represents a transient error
        /// </summary>
        private static bool IsTransientError(Exception ex)
        {
            return ex switch
            {
                TaskCanceledException => true,
                TimeoutException => true,
                HttpRequestException httpEx when httpEx.Message.Contains("timeout") => true,
                _ => false
            };
        }

        /// <summary>
        /// Calculates delay for retry with exponential backoff
        /// </summary>
        private static TimeSpan CalculateRetryDelay(int attempt)
        {
            // Base delay: 1 second, exponential backoff with jitter
            var baseDelay = TimeSpan.FromSeconds(1);
            var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
            
            // Add jitter (±25%)
            var jitter = Random.Shared.NextDouble() * 0.5 + 0.75; // 0.75 to 1.25
            var finalDelay = TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitter);
            
            // Cap at 15 seconds
            return finalDelay > TimeSpan.FromSeconds(15) ? TimeSpan.FromSeconds(15) : finalDelay;
        }

        /// <summary>
        /// Analyzes HTTP exceptions to provide detailed error information
        /// </summary>
        private ErrorInfo AnalyzeHttpException(HttpRequestException ex, string context)
        {
            var message = ex.Message.ToLower();

            if (message.Contains("401") || message.Contains("unauthorized"))
            {
                return CommonErrors.InvalidApiKey();
            }

            if (message.Contains("429") || message.Contains("too many requests"))
            {
                return CommonErrors.ApiRateLimitExceeded();
            }

            if (message.Contains("403") || message.Contains("forbidden"))
            {
                return new ErrorInfo
                {
                    Category = ErrorCategory.ApiAuthentication,
                    Severity = ErrorSeverity.High,
                    UserMessage = "API key không có quyền truy cập.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng kiểm tra API key và billing account Google Cloud.",
                    OriginalException = ex
                };
            }

            if (message.Contains("400") || message.Contains("bad request"))
            {
                return new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Dữ liệu gửi lên API không hợp lệ.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng kiểm tra nội dung text đầu vào.",
                    OriginalException = ex,
                    ContextInfo = context
                };
            }

            if (message.Contains("timeout") || message.Contains("no response"))
            {
                return CommonErrors.NetworkConnectionError();
            }

            return new ErrorInfo
            {
                Category = ErrorCategory.ApiProcessing,
                Severity = ErrorSeverity.High,
                UserMessage = "Lỗi kết nối API Gemini.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Vui lòng thử lại sau vài phút.",
                OriginalException = ex,
                ContextInfo = context
            };
        }

        /// <summary>
        /// Calculates cosine similarity between two embedding vectors
        /// </summary>
        /// <param name="vector1">First embedding vector</param>
        /// <param name="vector2">Second embedding vector</param>
        /// <returns>Cosine similarity value between -1 and 1</returns>
        public static double CalculateCosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            if (vector1.Length == 0)
                return 0.0;

            // Use vectorized operations for better performance
            var span1 = vector1.AsSpan();
            var span2 = vector2.AsSpan();
            
            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            // Process in chunks that fit SIMD registers (Vector<float>.Count elements at a time)
            int vectorSize = Vector<float>.Count;
            int i = 0;
            
            // Vectorized processing for bulk of the data
            for (; i <= vector1.Length - vectorSize; i += vectorSize)
            {
                var v1 = new Vector<float>(span1.Slice(i, vectorSize));
                var v2 = new Vector<float>(span2.Slice(i, vectorSize));
                
                dotProduct += Vector.Dot(v1, v2);
                magnitude1 += Vector.Dot(v1, v1);
                magnitude2 += Vector.Dot(v2, v2);
            }
            
            // Handle remaining elements
            for (; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }

    /// <summary>
    /// Statistics about embedding processing with retry information
    /// </summary>
    public record EmbeddingProcessingStats
    {
        public int TotalRows { get; init; }
        public int SuccessfulRows { get; init; }
        public int FailedRows { get; init; }
        public List<string> ErrorMessages { get; init; } = new();
        public double SuccessRate { get; init; }
        public int RetryAttempts { get; init; }
    }
} 