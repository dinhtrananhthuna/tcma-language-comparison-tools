using System;
using System.Collections.Generic;
using System.Linq;
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

            // Gom thành JSON array
            var inputArray = rowList.Select(r => new { ContentId = r.ContentId, Content = r.Content }).ToList();
            var inputJson = System.Text.Json.JsonSerializer.Serialize(inputArray);

            // Prompt yêu cầu AI trả về JSON array mapping ContentId → TranslatedContent
            var prompt = $@"You are a professional translator. Translate the following JSON array from {sourceLang} to {targetLang}.
Return a JSON array with the same ContentId and the translated English in TranslatedContent.
Input: {inputJson}
Output format example: [{{""ContentId"":""..."", ""TranslatedContent"":""...""}}, ...]";

            string aiText = string.Empty;
            try
            {
                var response = await _model.GenerateContent(prompt);
                aiText = response?.Text?.Trim() ?? string.Empty;
                // Tìm đoạn JSON trong kết quả trả về
                int startIdx = aiText.IndexOf('[');
                int endIdx = aiText.LastIndexOf(']');
                if (startIdx == -1 || endIdx == -1 || endIdx <= startIdx)
                {
                    Console.WriteLine("❌ [AI Translate Error] AI không trả về JSON hợp lệ.");
                    Console.WriteLine($"AI Response: {aiText}");
                    Console.WriteLine($"Input JSON length: {inputJson.Length}");
                    return OperationResult<List<TranslationResult>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.ApiProcessing,
                        Severity = ErrorSeverity.High,
                        UserMessage = "AI không trả về JSON hợp lệ.",
                        TechnicalDetails = aiText,
                        SuggestedAction = "Vui lòng kiểm tra lại prompt hoặc thử lại."
                    });
                }
                var jsonPart = aiText.Substring(startIdx, endIdx - startIdx + 1);
                var results = System.Text.Json.JsonSerializer.Deserialize<List<TranslationResult>>(jsonPart);
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
                progress?.Report($"Đã dịch {results.Count}/{rowList.Count} dòng (batch JSON).");
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
    }
} 