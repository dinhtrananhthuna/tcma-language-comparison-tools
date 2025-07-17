using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mscc.GenerativeAI;
using DotNetEnv;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Core.Services;
using Microsoft.Extensions.Configuration;

namespace Tcma.LanguageComparison.Core
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Check for test command
            if (args.Length > 0 && args[0] == "test")
            {
                await RunAlignmentTest();
                return;
            }

            Console.WriteLine("=== TCMA Language Comparison Tool ===");
            Console.WriteLine("Công cụ so sánh nội dung đa ngôn ngữ sử dụng AI embeddings\n");

            // Load configuration
            var configService = new ConfigurationService();
            var config = configService.Configuration;
            
            configService.DisplayConfiguration();

            // Check if Gemini API key is provided
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("\n❌ GEMINI_API_KEY environment variable chưa được thiết lập.");
                Console.WriteLine("Vui lòng thiết lập API key: set GEMINI_API_KEY=your_api_key_here");
                Console.WriteLine("\nHoặc chạy lệnh sau (thay your_actual_api_key):");
                Console.WriteLine("$env:GEMINI_API_KEY=\"your_actual_api_key\"");
                return;
            }

            try
            {
                // Initialize services
                var csvService = new CsvReaderService();
                var preprocessingService = new TextPreprocessingService();
                var embeddingService = new GeminiEmbeddingService(apiKey, config.LanguageComparison.MaxEmbeddingBatchSize);
                var matchingService = new ContentMatchingService(config.LanguageComparison.SimilarityThreshold);

                // Create progress reporter
                var progress = new Progress<string>(message => 
                {
                    Console.WriteLine($"[Progress] {message}");
                });

                // Test API connection
                Console.WriteLine("\nKiểm tra kết nối Gemini API...");
                var connectionResult = await embeddingService.TestConnectionAsync();
                if (!connectionResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Không thể kết nối với Gemini API: {connectionResult.Error?.UserMessage}");
                    Console.WriteLine($"Chi tiết: {connectionResult.Error?.TechnicalDetails}");
                    Console.WriteLine($"Giải pháp: {connectionResult.Error?.SuggestedAction}");
                    return;
                }
                Console.WriteLine("✓ Kết nối Gemini API thành công");

                // Look for sample CSV files
                var sampleDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "sample"));
                var englishFile = Path.Combine(sampleDir, "Lifecycle Management_20250603.csv");
                var koreanFile = Path.Combine(sampleDir, "Lifecycle Management_20250603-KR.csv");

                if (!File.Exists(englishFile) || !File.Exists(koreanFile))
                {
                    Console.WriteLine($"\n⚠ Không tìm thấy sample files:");
                    Console.WriteLine($"   English: {englishFile}");
                    Console.WriteLine($"   Korean: {koreanFile}");
                    Console.WriteLine("\nChạy demo với dữ liệu test thay thế...");
                    await RunSimpleDemo(embeddingService, progress);
                    return;
                }

                // Load CSV files
                Console.WriteLine($"\nĐọc CSV files...");
                Console.WriteLine($"English file: {englishFile}");
                Console.WriteLine($"Korean file: {koreanFile}");

                var refResult = await csvService.ReadContentRowsAsync(englishFile);
                if (!refResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Lỗi đọc file reference: {refResult.Error?.UserMessage}");
                    Console.WriteLine($"Chi tiết: {refResult.Error?.TechnicalDetails}");
                    return;
                }

                var targetResult = await csvService.ReadContentRowsAsync(koreanFile);
                if (!targetResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Lỗi đọc file target: {targetResult.Error?.UserMessage}");
                    Console.WriteLine($"Chi tiết: {targetResult.Error?.TechnicalDetails}");
                    return;
                }

                var referenceRows = refResult.Data!;
                var targetRows = targetResult.Data!;

                Console.WriteLine($"✓ Đã đọc {referenceRows.Count} reference rows và {targetRows.Count} target rows");

                // Preprocess content
                Console.WriteLine("\nXử lý nội dung (loại bỏ HTML tags)...");
                preprocessingService.ProcessContentRows(referenceRows);
                preprocessingService.ProcessContentRows(targetRows);

                // Apply demo row limit if configured
                var limitedRef = config.LanguageComparison.DemoRowLimit > 0 
                    ? referenceRows.Take(config.LanguageComparison.DemoRowLimit).ToList() 
                    : referenceRows;
                var limitedTarget = config.LanguageComparison.DemoRowLimit > 0 
                    ? targetRows.Take(config.LanguageComparison.DemoRowLimit).ToList() 
                    : targetRows;

                Console.WriteLine($"✓ Xử lý {limitedRef.Count} reference và {limitedTarget.Count} target rows" +
                                 (config.LanguageComparison.DemoRowLimit > 0 ? " (giới hạn cho demo)" : ""));

                // Generate embeddings
                Console.WriteLine("\nTạo embeddings cho nội dung reference...");
                var refEmbeddingResult = await embeddingService.GenerateEmbeddingsAsync(limitedRef, progress);
                if (!refEmbeddingResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Lỗi tạo embeddings cho reference: {refEmbeddingResult.Error?.UserMessage}");
                    return;
                }

                Console.WriteLine("\nTạo embeddings cho nội dung target...");
                var targetEmbeddingResult = await embeddingService.GenerateEmbeddingsAsync(limitedTarget, progress);
                if (!targetEmbeddingResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Lỗi tạo embeddings cho target: {targetEmbeddingResult.Error?.UserMessage}");
                    return;
                }

                var refStats = refEmbeddingResult.Data!;
                var targetStats = targetEmbeddingResult.Data!;

                Console.WriteLine($"\nThống kê embeddings:");
                Console.WriteLine($"Reference: {refStats.SuccessfulRows}/{refStats.TotalRows} thành công ({refStats.SuccessRate:F1}%)");
                Console.WriteLine($"Target: {targetStats.SuccessfulRows}/{targetStats.TotalRows} thành công ({targetStats.SuccessRate:F1}%)");

                if (refStats.SuccessfulRows == 0 || targetStats.SuccessfulRows == 0)
                {
                    Console.WriteLine("❌ Không thể tiếp tục vì không có embeddings thành công");
                    return;
                }

                // Perform line-by-line matching
                Console.WriteLine("\nThực hiện so sánh line-by-line...");
                var lineByLineResult = await matchingService.GenerateLineByLineReportAsync(limitedRef, limitedTarget, progress);
                if (!lineByLineResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Lỗi trong quá trình so sánh: {lineByLineResult.Error?.UserMessage}");
                    return;
                }

                var lineByLineResults = lineByLineResult.Data!;

                // Create MatchResult objects for statistics
                var matchResults = lineByLineResults.Select(r => new MatchResult
                {
                    ReferenceRow = r.CorrespondingReferenceRow ?? new ContentRow(),
                    MatchedRow = r.TargetRow,
                    SimilarityScore = r.LineByLineScore,
                    IsGoodMatch = r.IsGoodLineByLineMatch
                }).ToList();

                // Display statistics
                var stats = matchingService.GetMatchingStatistics(matchResults);
                Console.WriteLine("\n=== KẾT QUẢ SO SÁNH ===");
                Console.WriteLine($"Tổng số dòng được so sánh: {stats.TotalReferenceRows}");
                Console.WriteLine($"Matches tốt (>= threshold): {stats.GoodMatches}");
                Console.WriteLine($"Tỷ lệ match: {stats.MatchPercentage:F1}%");
                Console.WriteLine($"Điểm similarity trung bình: {stats.AverageSimilarityScore:F3}");
                Console.WriteLine($"\nPhân loại chất lượng:");
                Console.WriteLine($"  Cao (>= 0.8): {stats.HighQualityMatches}");
                Console.WriteLine($"  Trung bình (0.6-0.8): {stats.MediumQualityMatches}");
                Console.WriteLine($"  Thấp (0.4-0.6): {stats.LowQualityMatches}");
                Console.WriteLine($"  Kém (< 0.4): {stats.PoorQualityMatches}");

                // Show some examples
                Console.WriteLine("\n=== VÍ DỤ KẾT QUẢ ===");
                var examples = lineByLineResults.Take(Math.Min(5, lineByLineResults.Count)).ToList();
                
                foreach (var result in examples)
                {
                    Console.WriteLine($"\nDòng #{result.TargetRow.OriginalIndex + 1}:");
                    Console.WriteLine($"Target: {TruncateText(result.TargetRow.Content, 80)}");
                    Console.WriteLine($"Reference: {TruncateText(result.CorrespondingReferenceRow?.Content ?? "N/A", 80)}");
                    Console.WriteLine($"Score: {result.LineByLineScore:F3} | Quality: {result.Quality}");
                    
                    if (result.SuggestedMatch != null && !result.IsGoodLineByLineMatch)
                    {
                        Console.WriteLine($"Suggestion: {TruncateText(result.SuggestedMatch.ReferenceRow.Content, 80)} (Score: {result.SuggestedMatch.SimilarityScore:F3})");
                    }
                }

                // Export results
                Console.WriteLine("\nXuất kết quả...");
                var outputFile = Path.Combine(sampleDir, "Comparison_Results.csv");
                var exportData = lineByLineResults.Select((result, index) => new ContentRow
                {
                    ContentId = $"Line{index + 1}",
                    Content = CreateExportContent(result)
                }).ToList();

                var exportResult = await csvService.WriteContentRowsAsync(outputFile, exportData);
                if (exportResult.IsSuccess)
                {
                    Console.WriteLine($"✓ Đã xuất kết quả ra: {outputFile}");
                }
                else
                {
                    Console.WriteLine($"❌ Lỗi xuất file: {exportResult.Error?.UserMessage}");
                }

                Console.WriteLine("\n=== HOÀN THÀNH ===");
                Console.WriteLine("Nhấn Enter để thoát...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Lỗi không mong muốn: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task RunSimpleDemo(GeminiEmbeddingService embeddingService, IProgress<string> progress)
        {
            Console.WriteLine("\n=== DEMO ĐƠN GIẢN ===");
            
            // Test with simple content
            var testRef = new List<ContentRow>
            {
                new() { ContentId = "1", Content = "Hello world", OriginalIndex = 0 },
                new() { ContentId = "2", Content = "Good morning", OriginalIndex = 1 },
                new() { ContentId = "3", Content = "Thank you", OriginalIndex = 2 }
            };

            var testTarget = new List<ContentRow>
            {
                new() { ContentId = "1", Content = "안녕하세요", OriginalIndex = 0 },
                new() { ContentId = "2", Content = "좋은 아침입니다", OriginalIndex = 1 },
                new() { ContentId = "3", Content = "감사합니다", OriginalIndex = 2 }
            };

            // Set clean content
            foreach (var row in testRef.Concat(testTarget))
            {
                row.CleanContent = row.Content;
            }

            Console.WriteLine("Tạo embeddings cho demo content...");
            var refResult = await embeddingService.GenerateEmbeddingsAsync(testRef, progress);
            var targetResult = await embeddingService.GenerateEmbeddingsAsync(testTarget, progress);

            if (refResult.IsSuccess && targetResult.IsSuccess)
            {
                var matchingService = new ContentMatchingService(0.3);
                var lineByLineResult = await matchingService.GenerateLineByLineReportAsync(testRef, testTarget, progress);
                
                if (lineByLineResult.IsSuccess)
                {
                    Console.WriteLine("\nKết quả demo:");
                    foreach (var result in lineByLineResult.Data!)
                    {
                        Console.WriteLine($"'{result.TargetRow.Content}' <-> '{result.CorrespondingReferenceRow?.Content}' | Score: {result.LineByLineScore:F3}");
                    }
                }
            }
        }

        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            
            return text.Substring(0, maxLength - 3) + "...";
        }

        private static string CreateExportContent(LineByLineMatchResult result)
        {
            var parts = new List<string>
            {
                $"Target Line #{result.TargetRow.OriginalIndex + 1}: {result.TargetRow.Content}",
                $"Reference Content: {result.CorrespondingReferenceRow?.Content ?? "N/A"}",
                $"Similarity Score: {result.LineByLineScore:F3}",
                $"Quality: {result.Quality}"
            };
            
            if (result.SuggestedMatch != null && !result.IsGoodLineByLineMatch)
            {
                parts.Add($"Suggestion: {result.SuggestedMatch.ReferenceRow.Content} (Score: {result.SuggestedMatch.SimilarityScore:F3})");
            }
            else
            {
                parts.Add("Suggestion: N/A");
            }
            
            return string.Join(" ||| ", parts);
        }

        private static async Task RunAlignmentTest()
        {
            Console.WriteLine("🧪 TCMA Alignment Test Mode");
            Console.WriteLine(new string('=', 50));
            
            // Get API key
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("❌ GEMINI_API_KEY not set. Please set it first:");
                Console.WriteLine("$env:GEMINI_API_KEY=\"your_api_key_here\"");
                return;
            }
            
            Console.WriteLine("🔑 API Key found, starting test...\n");
            
            // Run alignment test
            await AlignmentTest.RunAlignmentTestAsync(apiKey);
            
            Console.WriteLine("\n🏁 Test completed!");
        }
    }
}
