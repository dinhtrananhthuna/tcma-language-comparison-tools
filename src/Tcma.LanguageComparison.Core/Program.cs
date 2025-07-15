using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Mscc.GenerativeAI;
using DotNetEnv;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Core.Services;

namespace Tcma.LanguageComparison.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TCMA Language Comparison Tools ===");
            Console.WriteLine("Phase 1: Core Logic & Proof of Concept\n");

            // Load environment variables from .env file
            LoadEnvironmentVariables();

            // API Key configuration
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("ERROR: GEMINI_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Gemini API key as an environment variable:");
                Console.WriteLine("set GEMINI_API_KEY=your_api_key_here");
                Console.WriteLine("Or create a .env file with: GEMINI_API_KEY=your_api_key_here");
                return;
            }

            try
            {
                await RunContentComparisonDemo(apiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Main demo function for content comparison
        /// </summary>
        static async Task RunContentComparisonDemo(string apiKey)
        {
            // Load configuration
            var configService = new ConfigurationService();
            var config = configService.Configuration;

            // Display current configuration
            configService.DisplayConfiguration();

            // Initialize services with configuration
            var embeddingService = new GeminiEmbeddingService(apiKey);
            var csvService = new CsvReaderService();
            var preprocessingService = new TextPreprocessingService();
            var matchingService = new ContentMatchingService(config.LanguageComparison.SimilarityThreshold);
            
            var progress = config.Output.ShowProgressMessages 
                ? new Progress<string>(message => Console.WriteLine($"[INFO] {message}"))
                : null;

            Console.WriteLine("✓ Khởi tạo thành công các services");

            // Test API connection
            Console.WriteLine("\nKiểm tra kết nối Gemini API...");
            var isConnected = await embeddingService.TestConnectionAsync();
            if (!isConnected)
            {
                Console.WriteLine("❌ Không thể kết nối với Gemini API. Vui lòng kiểm tra API key.");
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

            var referenceRows = await csvService.ReadContentRowsAsync(englishFile);
            var targetRows = await csvService.ReadContentRowsAsync(koreanFile);

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
            Console.WriteLine("\nTạo embeddings...");
            await embeddingService.GenerateEmbeddingsAsync(limitedRef, progress);
            await embeddingService.GenerateEmbeddingsAsync(limitedTarget, progress);

            // Find matches
            Console.WriteLine("\nTìm matches...");
            var matchResults = await matchingService.FindMatchesAsync(limitedRef, limitedTarget, progress);

            // Display results if configured
            if (config.Output.ShowDetailedResults)
            {
                DisplayMatchResults(matchResults);
            }

            // Generate and display statistics
            var stats = matchingService.GetMatchingStatistics(matchResults);
            DisplayStatistics(stats);

            // Create reordered file
            Console.WriteLine("\nTạo file đã sắp xếp lại...");
            var reorderedRows = matchingService.CreateReorderedTargetList(matchResults);
            var outputFile = Path.Combine(sampleDir, "Reordered_Korean_Output.csv");
            await csvService.WriteContentRowsAsync(outputFile, reorderedRows);
            Console.WriteLine($"✓ Đã tạo file: {outputFile}");
        }

        /// <summary>
        /// Simple demo with hardcoded text data
        /// </summary>
        static async Task RunSimpleDemo(GeminiEmbeddingService embeddingService, IProgress<string>? progress)
        {
            // Load configuration for simple demo
            var configService = new ConfigurationService();
            var config = configService.Configuration;

            // Create test data
            var referenceRows = new List<ContentRow>
            {
                new() { ContentId = "1", Content = "Hello, how are you today?", OriginalIndex = 0 },
                new() { ContentId = "2", Content = "The weather is beautiful today.", OriginalIndex = 1 },
                new() { ContentId = "3", Content = "Please save your work before closing.", OriginalIndex = 2 }
            };

            var targetRows = new List<ContentRow>
            {
                new() { ContentId = "A", Content = "작업을 저장한 후 닫으세요.", OriginalIndex = 0 }, // Korean for "save work"
                new() { ContentId = "B", Content = "안녕하세요, 오늘 어떠세요?", OriginalIndex = 1 }, // Korean for "hello"
                new() { ContentId = "C", Content = "오늘 날씨가 정말 좋네요.", OriginalIndex = 2 } // Korean for "weather"
            };

            var preprocessingService = new TextPreprocessingService();
            var matchingService = new ContentMatchingService(config.LanguageComparison.SimilarityThreshold);

            // Process content
            preprocessingService.ProcessContentRows(referenceRows);
            preprocessingService.ProcessContentRows(targetRows);

            // Generate embeddings
            await embeddingService.GenerateEmbeddingsAsync(referenceRows, progress);
            await embeddingService.GenerateEmbeddingsAsync(targetRows, progress);

            // Find matches
            var matchResults = await matchingService.FindMatchesAsync(referenceRows, targetRows, progress);

            // Display results
            DisplayMatchResults(matchResults);

            var stats = matchingService.GetMatchingStatistics(matchResults);
            DisplayStatistics(stats);
        }

        /// <summary>
        /// Load environment variables from .env file
        /// </summary>
        static void LoadEnvironmentVariables()
        {
            // Look for .env file in current directory, then parent directories
            Env.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), ".env")))
            {
                var rootDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
                var envFile = Path.Combine(rootDir, ".env");
                if (File.Exists(envFile))
                {
                    Env.Load(envFile);
                }
            }
        }

        /// <summary>
        /// Display match results in a formatted table
        /// </summary>
        static void DisplayMatchResults(IEnumerable<MatchResult> matchResults)
        {
            Console.WriteLine("\n=== MATCH RESULTS ===");
            Console.WriteLine($"{"Index",-5} {"Quality",-8} {"Score",-6} {"Reference Content",-40} {"Matched Content",-40}");
            Console.WriteLine(new string('=', 105));

            foreach (var result in matchResults)
            {
                var qualityColor = result.Quality switch
                {
                    MatchQuality.High => "HIGH",
                    MatchQuality.Medium => "MED",
                    MatchQuality.Low => "LOW",
                    _ => "POOR"
                };

                var refContent = result.ReferenceRow.CleanContent.Length > 37 
                    ? result.ReferenceRow.CleanContent[..37] + "..." 
                    : result.ReferenceRow.CleanContent;

                var matchedContent = result.MatchedRow?.CleanContent?.Length > 37
                    ? result.MatchedRow.CleanContent[..37] + "..."
                    : result.MatchedRow?.CleanContent ?? "[NO MATCH]";

                Console.WriteLine($"{result.ReferenceRow.OriginalIndex,-5} {qualityColor,-8} {result.SimilarityScore,-6:F3} {refContent,-40} {matchedContent,-40}");
            }
        }

        /// <summary>
        /// Display matching statistics
        /// </summary>
        static void DisplayStatistics(MatchingStatistics stats)
        {
            Console.WriteLine("\n=== STATISTICS ===");
            Console.WriteLine($"Total Reference Rows: {stats.TotalReferenceRows}");
            Console.WriteLine($"Good Matches: {stats.GoodMatches} ({stats.MatchPercentage:F1}%)");
            Console.WriteLine($"High Quality: {stats.HighQualityMatches}");
            Console.WriteLine($"Medium Quality: {stats.MediumQualityMatches}");
            Console.WriteLine($"Low Quality: {stats.LowQualityMatches}");
            Console.WriteLine($"Poor Quality: {stats.PoorQualityMatches}");
            Console.WriteLine($"Average Similarity: {stats.AverageSimilarityScore:F3}");
        }

        /// <summary>
        /// Calculate cosine similarity between two embedding vectors
        /// </summary>
        /// <param name="vector1">First embedding vector</param>
        /// <param name="vector2">Second embedding vector</param>
        /// <returns>Cosine similarity value between -1 and 1</returns>
        static double CalculateCosineSimilarity(IList<float> vector1, IList<float> vector2)
        {
            if (vector1.Count != vector2.Count)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Count; i++)
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
    }
}
