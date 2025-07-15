using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for matching and pairing reference and target files based on filename patterns
    /// </summary>
    public class FileMatchingService
    {
        // Pattern: {PageName}_xxx-{LanguageCode}.csv
        private static readonly Regex FileNamePattern = new(@"^(.+?)_.*?-([A-Z]{2})\.csv$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        /// <summary>
        /// Extracts the page name from a filename using the standard pattern
        /// </summary>
        /// <param name="fileName">The filename to extract from</param>
        /// <returns>The extracted page name, or the full filename if pattern doesn't match</returns>
        public static string ExtractPageName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var match = FileNamePattern.Match(fileName);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Fallback: remove extension and try to extract meaningful name
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            // Try simple pattern without language code
            var simpleMatch = Regex.Match(nameWithoutExt, @"^(.+?)_", RegexOptions.IgnoreCase);
            if (simpleMatch.Success)
            {
                return simpleMatch.Groups[1].Value;
            }

            return nameWithoutExt;
        }

        /// <summary>
        /// Extracts the language code from a filename
        /// </summary>
        /// <param name="fileName">The filename to extract from</param>
        /// <returns>The language code if found, otherwise empty string</returns>
        public static string ExtractLanguageCode(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var match = FileNamePattern.Match(fileName);
            return match.Success ? match.Groups[2].Value.ToUpperInvariant() : string.Empty;
        }

        /// <summary>
        /// Pairs reference and target files based on their page names
        /// </summary>
        /// <param name="referenceFiles">List of reference file paths</param>
        /// <param name="targetFiles">List of target file paths</param>
        /// <returns>OperationResult containing the file extraction result</returns>
        public async Task<OperationResult<FileExtractionResult>> PairFilesAsync(
            List<string> referenceFiles, 
            List<string> targetFiles)
        {
            try
            {
                if (referenceFiles == null || !referenceFiles.Any())
                {
                    return OperationResult<FileExtractionResult>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có file reference nào để ghép cặp.",
                        TechnicalDetails = "Reference files list is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo ZIP reference chứa file CSV."
                    });
                }

                if (targetFiles == null || !targetFiles.Any())
                {
                    return OperationResult<FileExtractionResult>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không có file target nào để ghép cặp.",
                        TechnicalDetails = "Target files list is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo ZIP target chứa file CSV."
                    });
                }

                var result = new FileExtractionResult();
                var pairedPages = new List<PageInfo>();
                var unpairedFiles = new List<string>();

                // Group files by page name
                var refGroups = referenceFiles
                    .Select(f => new { 
                        FilePath = f, 
                        FileName = Path.GetFileName(f),
                        PageName = ExtractPageName(Path.GetFileName(f)),
                        LanguageCode = ExtractLanguageCode(Path.GetFileName(f))
                    })
                    .Where(f => !string.IsNullOrEmpty(f.PageName))
                    .GroupBy(f => f.PageName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var targetGroups = targetFiles
                    .Select(f => new { 
                        FilePath = f, 
                        FileName = Path.GetFileName(f),
                        PageName = ExtractPageName(Path.GetFileName(f)),
                        LanguageCode = ExtractLanguageCode(Path.GetFileName(f))
                    })
                    .Where(f => !string.IsNullOrEmpty(f.PageName))
                    .GroupBy(f => f.PageName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Find matching pairs
                foreach (var refGroup in refGroups)
                {
                    var matchingTargetGroup = targetGroups
                        .FirstOrDefault(tg => string.Equals(tg.Key, refGroup.Key, StringComparison.OrdinalIgnoreCase));

                    if (matchingTargetGroup != null)
                    {
                        // Take the first file from each group (in case of multiple files with same page name)
                        var refFile = refGroup.First();
                        var targetFile = matchingTargetGroup.First();

                        var pageInfo = new PageInfo
                        {
                            PageName = refGroup.Key,
                            ReferenceFilePath = refFile.FilePath,
                            TargetFilePath = targetFile.FilePath,
                            Status = PageStatus.Ready
                        };

                        pairedPages.Add(pageInfo);

                        // Add remaining files to unpaired list
                        unpairedFiles.AddRange(refGroup.Skip(1).Select(f => f.FilePath));
                        unpairedFiles.AddRange(matchingTargetGroup.Skip(1).Select(f => f.FilePath));
                    }
                    else
                    {
                        // No matching target found
                        unpairedFiles.AddRange(refGroup.Select(f => f.FilePath));
                    }
                }

                // Add unmatched target files to unpaired list
                var matchedTargetPageNames = pairedPages.Select(p => p.PageName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unmatchedTargetGroups = targetGroups
                    .Where(tg => !matchedTargetPageNames.Contains(tg.Key))
                    .SelectMany(tg => tg.Select(f => f.FilePath));
                
                unpairedFiles.AddRange(unmatchedTargetGroups);

                // Add files that couldn't be parsed
                var unparsableRefFiles = referenceFiles
                    .Where(f => string.IsNullOrEmpty(ExtractPageName(Path.GetFileName(f))))
                    .ToList();
                
                var unparsableTargetFiles = targetFiles
                    .Where(f => string.IsNullOrEmpty(ExtractPageName(Path.GetFileName(f))))
                    .ToList();

                unpairedFiles.AddRange(unparsableRefFiles);
                unpairedFiles.AddRange(unparsableTargetFiles);

                // Validate results
                if (!pairedPages.Any())
                {
                    return OperationResult<FileExtractionResult>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không thể ghép cặp file nào dựa trên tên page.",
                        TechnicalDetails = $"No matching page names found. Reference groups: {refGroups.Count}, Target groups: {targetGroups.Count}",
                        SuggestedAction = "Vui lòng kiểm tra format tên file theo pattern: {{PageName}}_xxx-{{LanguageCode}}.csv"
                    });
                }

                result.Pages = pairedPages;
                result.UnpairedFiles = unpairedFiles.Distinct().ToList();

                return OperationResult<FileExtractionResult>.Success(result);
            }
            catch (Exception ex)
            {
                return OperationResult<FileExtractionResult>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không xác định khi ghép cặp files.",
                    TechnicalDetails = $"Unexpected error during file pairing: {ex.Message}",
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ."
                });
            }
        }

        /// <summary>
        /// Calculates a confidence score for how well two filenames might be paired
        /// </summary>
        /// <param name="file1">First filename</param>
        /// <param name="file2">Second filename</param>
        /// <returns>Confidence score between 0.0 and 1.0</returns>
        public static double CalculatePairingConfidence(string file1, string file2)
        {
            if (string.IsNullOrWhiteSpace(file1) || string.IsNullOrWhiteSpace(file2))
                return 0.0;

            var page1 = ExtractPageName(file1);
            var page2 = ExtractPageName(file2);

            if (string.IsNullOrEmpty(page1) || string.IsNullOrEmpty(page2))
                return 0.0;

            // Exact page name match
            if (string.Equals(page1, page2, StringComparison.OrdinalIgnoreCase))
            {
                var lang1 = ExtractLanguageCode(file1);
                var lang2 = ExtractLanguageCode(file2);

                // Perfect match if page names are same and language codes are different
                if (!string.IsNullOrEmpty(lang1) && !string.IsNullOrEmpty(lang2) && 
                    !string.Equals(lang1, lang2, StringComparison.OrdinalIgnoreCase))
                {
                    return 1.0;
                }

                // Good match if page names are same but no clear language distinction
                return 0.8;
            }

            // Partial match based on string similarity
            return CalculateStringSimilarity(page1, page2);
        }

        /// <summary>
        /// Calculates string similarity using a simple algorithm
        /// </summary>
        /// <param name="str1">First string</param>
        /// <param name="str2">Second string</param>
        /// <returns>Similarity score between 0.0 and 1.0</returns>
        private static double CalculateStringSimilarity(string str1, string str2)
        {
            if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
                return 0.0;

            var maxLength = Math.Max(str1.Length, str2.Length);
            if (maxLength == 0)
                return 1.0;

            var distance = CalculateLevenshteinDistance(str1.ToLowerInvariant(), str2.ToLowerInvariant());
            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// Calculates Levenshtein distance between two strings
        /// </summary>
        private static int CalculateLevenshteinDistance(string str1, string str2)
        {
            var matrix = new int[str1.Length + 1, str2.Length + 1];

            for (int i = 0; i <= str1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= str2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= str1.Length; i++)
            {
                for (int j = 1; j <= str2.Length; j++)
                {
                    var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[str1.Length, str2.Length];
        }

        /// <summary>
        /// Validates that a filename follows the expected pattern
        /// </summary>
        /// <param name="fileName">Filename to validate</param>
        /// <returns>True if filename follows the expected pattern</returns>
        public static bool IsValidFileNamePattern(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return FileNamePattern.IsMatch(fileName);
        }

        /// <summary>
        /// Gets suggested filename corrections for files that don't match the pattern
        /// </summary>
        /// <param name="fileName">Original filename</param>
        /// <returns>Suggested filename or null if no suggestion available</returns>
        public static string? SuggestFileNameCorrection(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (IsValidFileNamePattern(fileName))
                return null; // Already valid

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            // Try to suggest a correction based on common patterns
            // This is a simple implementation - could be enhanced with more sophisticated logic
            if (nameWithoutExt.Contains("_") && !nameWithoutExt.Contains("-"))
            {
                return $"{nameWithoutExt}-XX{extension}"; // Suggest adding language code
            }

            return null;
        }
    }
} 