using Mscc.GenerativeAI;
using Tcma.LanguageComparison.Core.Models;

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
        private const int MaxConcurrentRequests = 5; // Limit concurrent API calls

        /// <summary>
        /// Initializes the Gemini embedding service
        /// </summary>
        /// <param name="apiKey">Google AI API key</param>
        public GeminiEmbeddingService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            _googleAI = new GoogleAI(apiKey);
            _model = _googleAI.GenerativeModel(Model.TextEmbedding004);
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
        }

        /// <summary>
        /// Generates embedding vector for a single text
        /// </summary>
        /// <param name="text">Text content to generate embedding for</param>
        /// <returns>Embedding vector as float array</returns>
        /// <exception cref="InvalidOperationException">Thrown when API call fails</exception>
        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            await _semaphore.WaitAsync();
            try
            {
                var response = await _model.EmbedContent(text);
                var values = response.Embedding?.Values;
                
                if (values == null || !values.Any())
                {
                    throw new InvalidOperationException($"Failed to generate embedding for text: {text[..Math.Min(50, text.Length)]}...");
                }

                return values.ToArray();
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                throw new InvalidOperationException($"Error calling Gemini API: {ex.Message}", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Generates embeddings for multiple content rows
        /// </summary>
        /// <param name="contentRows">Content rows to process</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        public async Task GenerateEmbeddingsAsync(
            IEnumerable<ContentRow> contentRows, 
            IProgress<string>? progressCallback = null)
        {
            var rows = contentRows.ToList();
            var processed = 0;
            var total = rows.Count;

            progressCallback?.Report($"Bắt đầu tạo embeddings cho {total} nội dung...");

            var tasks = rows.Where(row => !string.IsNullOrWhiteSpace(row.CleanContent))
                          .Select(async row =>
                          {
                              try
                              {
                                  row.EmbeddingVector = await GetEmbeddingAsync(row.CleanContent);
                                  var currentProgress = Interlocked.Increment(ref processed);
                                  
                                  if (currentProgress % 10 == 0 || currentProgress == total)
                                  {
                                      progressCallback?.Report($"Đã xử lý {currentProgress}/{total} nội dung...");
                                  }
                              }
                              catch (Exception ex)
                              {
                                  progressCallback?.Report($"Lỗi khi xử lý ContentId {row.ContentId}: {ex.Message}");
                                  // Keep EmbeddingVector as null to indicate failure
                              }
                          });

            await Task.WhenAll(tasks);
            progressCallback?.Report($"Hoàn thành! Đã tạo embeddings cho {processed}/{total} nội dung.");
        }

        /// <summary>
        /// Tests the API connection
        /// </summary>
        /// <returns>True if API is accessible, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await GetEmbeddingAsync("Test connection");
                return true;
            }
            catch
            {
                return false;
            }
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

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Length; i++)
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
} 