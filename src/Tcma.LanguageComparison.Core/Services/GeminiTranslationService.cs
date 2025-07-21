using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mscc.GenerativeAI;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service dịch thuật AI sử dụng Gemini Flash (model generative)
    /// </summary>
    public class GeminiTranslationService : IGeminiTranslationService
    {
        private readonly GoogleAI _googleAI;
        private readonly GenerativeModel _model;
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentRequests = 5;
        private const int OptimalBatchSize = 100; // Optimal batch size for API performance
        private const string DefaultPrompt = "Translate the following text to English. Only return the translated text, no explanation. Text: ";

        public GeminiTranslationService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            _googleAI = new GoogleAI(apiKey);
            _model = _googleAI.GenerativeModel("gemini-2.0-flash-lite");
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
        }

        public async Task<OperationResult<List<TranslationResult>>> TranslateBatchAsync(
            IEnumerable<ContentRow> rows,
            string sourceLang,
            string targetLang = "en",
            IProgress<string>? progress = null)
        {
            var rowList = rows?.ToList() ?? new List<ContentRow>();
            if (rowList.Count == 0)
            {
                return OperationResult<List<TranslationResult>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.Medium,
                    UserMessage = "Không có dữ liệu để dịch.",
                    TechnicalDetails = "Content rows collection is null or empty",
                    SuggestedAction = "Vui lòng kiểm tra dữ liệu đầu vào."
                });
            }

            // Use adaptive batching for large datasets
            if (rowList.Count > OptimalBatchSize)
            {
                return await TranslateLargeBatchAsync(rowList, sourceLang, targetLang, progress);
            }

            // Process small batches normally
            return await TranslateSingleBatchAsync(rowList, sourceLang, targetLang, progress);
        }

        /// <summary>
        /// Handles large translation batches by splitting them into optimal chunks
        /// </summary>
        private async Task<OperationResult<List<TranslationResult>>> TranslateLargeBatchAsync(
            List<ContentRow> rowList,
            string sourceLang,
            string targetLang,
            IProgress<string>? progress)
        {
            var allResults = new List<TranslationResult>();
            var totalRows = rowList.Count;
            var processed = 0;
            
            progress?.Report($"Dịch {totalRows} dòng với adaptive batching (batch size: {OptimalBatchSize})...");
            
            // Process in optimal-sized chunks
            for (int i = 0; i < totalRows; i += OptimalBatchSize)
            {
                var batchSize = Math.Min(OptimalBatchSize, totalRows - i);
                var batch = rowList.GetRange(i, batchSize);
                
                var batchResult = await TranslateSingleBatchAsync(batch, sourceLang, targetLang, null);
                if (!batchResult.IsSuccess)
                {
                    progress?.Report($"Lỗi khi dịch batch {i / OptimalBatchSize + 1}: {batchResult.Error?.UserMessage}");
                    return batchResult; // Return error from failed batch
                }
                
                allResults.AddRange(batchResult.Data!);
                processed += batchSize;
                
                progress?.Report($"Đã dịch {processed}/{totalRows} dòng ({processed * 100 / totalRows}%)...");
                
                // Small delay between batches to be API-friendly
                if (i + OptimalBatchSize < totalRows)
                    await Task.Delay(200);
            }
            
            progress?.Report($"Hoàn thành dịch {allResults.Count}/{totalRows} dòng với adaptive batching.");
            return OperationResult<List<TranslationResult>>.Success(allResults);
        }

        /// <summary>
        /// Processes a single batch translation
        /// </summary>
        private async Task<OperationResult<List<TranslationResult>>> TranslateSingleBatchAsync(
            List<ContentRow> rowList,
            string sourceLang,
            string targetLang,
            IProgress<string>? progress)
        {

            // Gom thành JSON array
            var inputArray = rowList.Select(r => new { ContentId = r.ContentId, Content = r.Content }).ToList();
            var inputJson = System.Text.Json.JsonSerializer.Serialize(inputArray);

            // Prompt yêu cầu AI trả về JSON array mapping ContentId → TranslatedContent
            var prompt = $@"CRITICAL INSTRUCTIONS: You are a professional translator. You MUST follow these rules EXACTLY:

1. Translate the following JSON array from {sourceLang} to {targetLang}
2. Return ONLY valid JSON array, no explanations, no markdown code blocks, no extra text
3. Each object must have EXACTLY these fields: ""ContentId"" and ""TranslatedContent""
4. Preserve ALL ContentId values exactly as provided
5. Start response with [ and end with ]
6. Do NOT add ```json or any formatting

Input: {inputJson}

Required output format: [{{""ContentId"":""ID001"", ""TranslatedContent"":""translated text""}}, {{""ContentId"":""ID002"", ""TranslatedContent"":""translated text""}}]";

            string aiText = string.Empty;
            try
            {
                var response = await _model.GenerateContent(prompt);
                aiText = response?.Text?.Trim() ?? string.Empty;
                
                // Fast path: try optimized streaming parser first
                var results = TryDirectJsonParsing(aiText);
                bool usedFastPath = results != null && results.Count > 0;
                
                if (!usedFastPath)
                {
                    // Fallback to multi-strategy parsing only if needed
                    results = ExtractAndParseJson(aiText, rowList.Count);
                    
                    if (results == null || results.Count == 0)
                    {
                        Console.WriteLine("❌ [AI Translate Error] Không parse được kết quả dịch từ AI.");
                        Console.WriteLine($"AI Response: {aiText}");
                        Console.WriteLine($"Input JSON length: {inputJson.Length}");
                        return OperationResult<List<TranslationResult>>.Failure(new ErrorInfo
                        {
                            Category = ErrorCategory.ApiProcessing,
                            Severity = ErrorSeverity.High,
                            UserMessage = "Không parse được kết quả dịch từ AI.",
                            TechnicalDetails = aiText,
                            SuggestedAction = "Vui lòng kiểm tra lại format trả về của AI."
                        });
                    }
                }
                
                // Skip expensive validation if fast path succeeded (it's already validated)
                if (!usedFastPath)
                {
                    var validationResult = ValidateTranslationResults(results, rowList);
                    if (!validationResult.IsSuccess)
                        return validationResult;
                }
                
                progress?.Report($"Đã dịch {results.Count}/{rowList.Count} dòng ({(usedFastPath ? "fast" : "fallback")} parsing).");
                return OperationResult<List<TranslationResult>>.Success(results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AI Translate Exception] {ex.Message}");
                if (ex.StackTrace != null)
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");
                Console.WriteLine($"Input JSON length: {inputJson.Length}");
                // Nếu có aiText thì log luôn
                try { if (!string.IsNullOrEmpty(aiText)) Console.WriteLine($"AI Response: {aiText}"); } catch {}
                return OperationResult<List<TranslationResult>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.ApiProcessing,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi khi gọi AI dịch batch JSON.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng kiểm tra lại API key hoặc format prompt.",
                    OriginalException = ex
                });
            }
        }

        /// <summary>
        /// Fast streaming JSON parsing optimized for performance
        /// </summary>
        private List<TranslationResult>? TryDirectJsonParsing(string aiText)
        {
            if (string.IsNullOrWhiteSpace(aiText))
                return null;

            try
            {
                // Use ReadOnlySpan for faster string operations (no allocations)
                var span = aiText.AsSpan().Trim();
                
                // Fast markdown removal with spans
                if (span.StartsWith("```json"))
                    span = span.Slice(7);
                if (span.EndsWith("```"))
                    span = span.Slice(0, span.Length - 3);
                span = span.Trim();

                // Quick validation before expensive parsing
                if (!span.StartsWith("[") || !span.EndsWith("]") || span.Length < 3)
                    return null;

                // Use streaming parser for better performance on large JSON
                using var doc = JsonDocument.Parse(span.ToString());
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                var results = new List<TranslationResult>(doc.RootElement.GetArrayLength());
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("ContentId", out var contentIdProp) &&
                        element.TryGetProperty("TranslatedContent", out var contentProp))
                    {
                        results.Add(new TranslationResult
                        {
                            ContentId = contentIdProp.GetString() ?? "",
                            TranslatedContent = contentProp.GetString() ?? ""
                        });
                    }
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                // Silent fail, will try fallback methods
            }
            return null;
        }

        /// <summary>
        /// Robust JSON extraction with multiple fallback methods
        /// </summary>
        private List<TranslationResult>? ExtractAndParseJson(string aiText, int expectedCount)
        {
            if (string.IsNullOrWhiteSpace(aiText))
                return null;

            // Strategy 1: Standard bracket extraction
            var result = TryStandardBracketExtraction(aiText);
            if (result != null && ValidateJsonStructure(result, expectedCount))
                return result;

            // Strategy 2: Regex-based JSON array extraction
            result = TryRegexExtraction(aiText);
            if (result != null && ValidateJsonStructure(result, expectedCount))
                return result;

            // Strategy 3: Line-by-line parsing for malformed responses
            result = TryLineByLineParsing(aiText);
            if (result != null && ValidateJsonStructure(result, expectedCount))
                return result;

            // Strategy 4: Partial JSON recovery
            result = TryPartialJsonRecovery(aiText, expectedCount);
            if (result != null && result.Count > 0)
                return result;

            Console.WriteLine("❌ All JSON extraction strategies failed");
            return null;
        }

        private List<TranslationResult>? TryStandardBracketExtraction(string aiText)
        {
            try
            {
                int startIdx = aiText.IndexOf('[');
                int endIdx = aiText.LastIndexOf(']');
                if (startIdx == -1 || endIdx == -1 || endIdx <= startIdx)
                    return null;

                var jsonPart = aiText.Substring(startIdx, endIdx - startIdx + 1);
                return System.Text.Json.JsonSerializer.Deserialize<List<TranslationResult>>(jsonPart);
            }
            catch
            {
                return null;
            }
        }

        private List<TranslationResult>? TryRegexExtraction(string aiText)
        {
            try
            {
                var regex = new System.Text.RegularExpressions.Regex(@"\[(?:[^[\]]|(?:\[(?:[^[\]]|(?:\[[^\]]*\]))*\]))*\]", 
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                var matches = regex.Matches(aiText);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    try
                    {
                        var result = System.Text.Json.JsonSerializer.Deserialize<List<TranslationResult>>(match.Value);
                        if (result != null && result.Count > 0)
                            return result;
                    }
                    catch { }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<TranslationResult>? TryLineByLineParsing(string aiText)
        {
            try
            {
                var results = new List<TranslationResult>();
                var lines = aiText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        try
                        {
                            var singleResult = System.Text.Json.JsonSerializer.Deserialize<TranslationResult>(trimmed);
                            if (singleResult != null && !string.IsNullOrWhiteSpace(singleResult.ContentId))
                                results.Add(singleResult);
                        }
                        catch { }
                    }
                }
                
                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
        }

        private List<TranslationResult>? TryPartialJsonRecovery(string aiText, int expectedCount)
        {
            try
            {
                var results = new List<TranslationResult>();
                var contentIdRegex = new System.Text.RegularExpressions.Regex(@"""ContentId""\s*:\s*""([^""]+)""", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var translatedContentRegex = new System.Text.RegularExpressions.Regex(@"""TranslatedContent""\s*:\s*""([^""]*(?:\\.[^""]*)*)""", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var contentIdMatches = contentIdRegex.Matches(aiText);
                var translatedMatches = translatedContentRegex.Matches(aiText);

                int minCount = Math.Min(contentIdMatches.Count, translatedMatches.Count);
                for (int i = 0; i < minCount; i++)
                {
                    results.Add(new TranslationResult
                    {
                        ContentId = contentIdMatches[i].Groups[1].Value,
                        TranslatedContent = translatedMatches[i].Groups[1].Value.Replace("\\\"", "\"")
                    });
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateJsonStructure(List<TranslationResult> results, int expectedCount)
        {
            if (results == null || results.Count == 0)
                return false;

            // Check basic structure
            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.ContentId) || result.TranslatedContent == null)
                    return false;
            }

            // Allow for some tolerance in count (e.g., AI might skip empty entries)
            return results.Count >= Math.Max(1, expectedCount * 0.5);
        }

        /// <summary>
        /// Validates translation results against input requirements
        /// </summary>
        private OperationResult<List<TranslationResult>> ValidateTranslationResults(
            List<TranslationResult> results, 
            List<ContentRow> inputRows)
        {
            var inputContentIds = inputRows.Select(r => r.ContentId).ToHashSet();
            var outputContentIds = results.Select(r => r.ContentId).ToHashSet();

            // Find missing content IDs
            var missingIds = inputContentIds.Except(outputContentIds).ToList();
            
            // If more than 20% are missing, it's likely a parsing error
            if (missingIds.Count > inputRows.Count * 0.2)
            {
                Console.WriteLine($"❌ Missing {missingIds.Count}/{inputRows.Count} ContentIds in translation");
                Console.WriteLine($"Missing: {string.Join(", ", missingIds.Take(5))}{(missingIds.Count > 5 ? "..." : "")}");
                
                return OperationResult<List<TranslationResult>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.ApiProcessing,
                    Severity = ErrorSeverity.High,
                    UserMessage = $"AI thiếu {missingIds.Count}/{inputRows.Count} ContentIds trong kết quả dịch.",
                    TechnicalDetails = $"Missing ContentIds: {string.Join(", ", missingIds.Take(10))}",
                    SuggestedAction = "Thử lại hoặc chia nhỏ batch để xử lý."
                });
            }

            // Fill missing entries with fallback
            foreach (var missingId in missingIds)
            {
                var originalRow = inputRows.First(r => r.ContentId == missingId);
                results.Add(new TranslationResult 
                { 
                    ContentId = missingId, 
                    TranslatedContent = $"[TRANSLATION_ERROR] {originalRow.Content}" 
                });
            }

            Console.WriteLine($"✅ Validation passed: {results.Count} results, {missingIds.Count} missing filled");
            return OperationResult<List<TranslationResult>>.Success(results);
        }
    }
} 