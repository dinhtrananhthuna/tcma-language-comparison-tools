using System.Collections.Generic;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Interface cho dịch thuật AI sử dụng Gemini Flash
    /// </summary>
    public interface IGeminiTranslationService
    {
        /// <summary>
        /// Dịch hàng loạt nội dung sang tiếng Anh, giữ nguyên ContentId
        /// </summary>
        /// <param name="rows">Danh sách ContentRow (chỉ dùng ContentId, Content)</param>
        /// <param name="sourceLang">Mã ngôn ngữ nguồn (ví dụ: "de", "vi", ...)</param>
        /// <param name="targetLang">Mã ngôn ngữ đích (luôn là "en")</param>
        /// <returns>Danh sách TranslationResult (ContentId, TranslatedContent)</returns>
        Task<OperationResult<List<TranslationResult>>> TranslateBatchAsync(
            IEnumerable<ContentRow> rows,
            string sourceLang,
            string targetLang = "en",
            IProgress<string>? progress = null);
    }

    /// <summary>
    /// Kết quả dịch cho một dòng nội dung
    /// </summary>
    public record TranslationResult
    {
        public string ContentId { get; init; } = string.Empty;
        public string TranslatedContent { get; init; } = string.Empty;
    }
} 