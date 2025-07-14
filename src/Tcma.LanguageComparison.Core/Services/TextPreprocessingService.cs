using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for preprocessing text content before embedding generation
    /// </summary>
    public class TextPreprocessingService
    {
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SpecialCharsRegex = new(@"[^\w\s\u4e00-\u9fff\uac00-\ud7af]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Cleans text by removing HTML tags and normalizing whitespace
        /// </summary>
        /// <param name="content">Raw content that may contain HTML</param>
        /// <returns>Clean text suitable for embedding generation</returns>
        public string CleanContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            try
            {
                // Method 1: Use HtmlAgilityPack for robust HTML parsing
                var cleanText = StripHtmlWithHtmlAgilityPack(content);
                
                // Fallback to regex if HAP fails
                if (string.IsNullOrWhiteSpace(cleanText))
                {
                    cleanText = StripHtmlWithRegex(content);
                }

                return NormalizeText(cleanText);
            }
            catch
            {
                // Fallback to regex method if anything fails
                return NormalizeText(StripHtmlWithRegex(content));
            }
        }

        /// <summary>
        /// Strips HTML tags using HtmlAgilityPack (more robust)
        /// </summary>
        private string StripHtmlWithHtmlAgilityPack(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc.DocumentNode.InnerText ?? string.Empty;
        }

        /// <summary>
        /// Strips HTML tags using regex (fallback method)
        /// </summary>
        private string StripHtmlWithRegex(string html)
        {
            // Remove HTML tags
            var withoutTags = Regex.Replace(html, @"<[^>]*>", " ", RegexOptions.IgnoreCase);
            
            // Decode common HTML entities
            return withoutTags
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&apos;", "'");
        }

        /// <summary>
        /// Normalizes text by cleaning whitespace and removing unnecessary characters
        /// </summary>
        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // Normalize whitespace
            text = WhitespaceRegex.Replace(text, " ");
            
            // Trim
            text = text.Trim();

            // Remove excessive special characters but keep basic punctuation
            // Keep: letters, numbers, spaces, CJK characters (Chinese, Japanese, Korean)
            text = SpecialCharsRegex.Replace(text, " ");

            // Final whitespace cleanup
            return WhitespaceRegex.Replace(text, " ").Trim();
        }

        /// <summary>
        /// Processes a batch of content rows to set their CleanContent
        /// </summary>
        /// <param name="contentRows">Content rows to process</param>
        public void ProcessContentRows(IEnumerable<Models.ContentRow> contentRows)
        {
            foreach (var row in contentRows)
            {
                row.CleanContent = CleanContent(row.Content);
            }
        }

        /// <summary>
        /// Validates if the cleaned content is suitable for embedding generation
        /// </summary>
        /// <param name="cleanContent">Cleaned content text</param>
        /// <returns>True if content is suitable for processing</returns>
        public bool IsContentValid(string cleanContent)
        {
            return !string.IsNullOrWhiteSpace(cleanContent) && 
                   cleanContent.Length >= 3 && // Minimum length
                   cleanContent.Length <= 8000; // Maximum length for API
        }
    }
} 