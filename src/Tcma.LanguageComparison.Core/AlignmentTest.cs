using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Tcma.LanguageComparison.Core.Services;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core
{
    /// <summary>
    /// Test class để kiểm tra thuật toán alignment
    /// </summary>
    public static class AlignmentTest
    {
        public static async Task RunAlignmentTestAsync(string geminiApiKey)
        {
            try
            {
                Console.WriteLine("🧪 Starting Alignment Test");
                Console.WriteLine(new string('=', 50));
                
                // File paths
                var refFile = "sample/NX Essentials Google Ads Page_20250529-EN.csv";
                var targetFile = "sample/NX Essentials Google Ads Page_20250529-ZH.csv";
                var outputFile = "test_output_aligned.csv";
                
                // Services
                var csvService = new CsvReaderService();
                var geminiService = new GeminiEmbeddingService(geminiApiKey);
                var preprocessingService = new TextPreprocessingService();
                var matchingService = new ContentMatchingService(0.35);
                var translationService = new GeminiTranslationService(geminiApiKey);
                
                // Load files
                Console.WriteLine("📂 Loading reference file (EN)...");
                var refResult = await csvService.ReadContentRowsAsync(refFile);
                if (!refResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to load reference file: {refResult.Error?.UserMessage}");
                    return;
                }
                var referenceRows = refResult.Data!;
                Console.WriteLine($"   Loaded {referenceRows.Count} reference rows");
                
                Console.WriteLine("📂 Loading target file (DE)...");
                var targetResult = await csvService.ReadContentRowsAsync(targetFile);
                if (!targetResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to load target file: {targetResult.Error?.UserMessage}");
                    return;
                }
                var targetRows = targetResult.Data!;
                Console.WriteLine($"   Loaded {targetRows.Count} target rows");
                
                // Lưu trữ nội dung gốc trước khi dịch
                var originalTargetRows = targetRows.ToList();
                
                // Dịch targetRows sang tiếng Anh
                Console.WriteLine("🌐 Translating target content to English using Gemini Flash...");
                var translationProgress = new Progress<string>(msg => Console.WriteLine($"[Translate] {msg}"));
                var translateResult = await translationService.TranslateBatchAsync(targetRows, "zh", "en", translationProgress);
                if (!translateResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to translate target: {translateResult.Error?.UserMessage}");
                    return;
                }
                var translated = translateResult.Data!;
                var translatedDict = translated.ToDictionary(t => t.ContentId, t => t.TranslatedContent);
                for (int i = 0; i < targetRows.Count; i++)
                {
                    var row = targetRows[i];
                    if (translatedDict.TryGetValue(row.ContentId, out var trans))
                    {
                        targetRows[i] = row with { Content = trans };
                    }
                }
                Console.WriteLine("🌐 Translation completed. Proceeding to preprocessing and embedding...");
                
                // Create reference ContentId map
                var refContentMap = referenceRows.ToDictionary(r => r.ContentId, r => r.Content);
                
                // Preprocessing
                Console.WriteLine("\n🔧 Preprocessing content...");
                preprocessingService.ProcessContentRows(referenceRows);
                preprocessingService.ProcessContentRows(targetRows);
                
                // Generate embeddings
                Console.WriteLine("🧠 Generating embeddings for reference...");
                var refEmbResult = await geminiService.GenerateEmbeddingsAsync(referenceRows, null);
                if (!refEmbResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to generate reference embeddings: {refEmbResult.Error?.UserMessage}");
                    return;
                }
                
                Console.WriteLine("🧠 Generating embeddings for target...");
                var targetEmbResult = await geminiService.GenerateEmbeddingsAsync(targetRows, null);
                if (!targetEmbResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to generate target embeddings: {targetEmbResult.Error?.UserMessage}");
                    return;
                }
                
                // Generate aligned display data
                Console.WriteLine("\n⚡ Running alignment algorithm...");
                var alignedData = await matchingService.GenerateAlignedDisplayDataAsync(referenceRows, targetRows, originalTargetRows, translated, null);
                Console.WriteLine($"   Generated {alignedData.Count} aligned display rows");
                
                // Export result
                Console.WriteLine("💾 Exporting aligned result...");
                var exportResult = await csvService.ExportAlignedDisplayRowsAsync(outputFile, alignedData);
                if (!exportResult.IsSuccess)
                {
                    Console.WriteLine($"❌ Failed to export: {exportResult.Error?.UserMessage}");
                    return;
                }
                
                // Analyze results
                Console.WriteLine("\n📊 Analyzing alignment results...");
                await AnalyzeAlignmentResults(refContentMap, outputFile, alignedData, referenceRows);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static async Task AnalyzeAlignmentResults(
            Dictionary<string, string> refContentMap, 
            string outputFile,
            List<AlignedDisplayRow> alignedData,
            List<ContentRow> referenceRows)
        {
            try
            {
                // Analyze in-memory aligned data first
                Console.WriteLine("🔍 Analyzing in-memory alignment data:");
                
                var matchedRows = alignedData.Where(r => r.Status == "Matched").ToList();
                var missingRows = alignedData.Where(r => r.Status == "Missing").ToList();
                var unmatchedTargetRows = alignedData.Where(r => r.Status == "Unmatched Target").ToList();
                
                Console.WriteLine($"   ✅ Matched rows: {matchedRows.Count}");
                Console.WriteLine($"   ❌ Missing rows: {missingRows.Count}");
                Console.WriteLine($"   🔄 Unmatched target rows: {unmatchedTargetRows.Count}");
                
                // Check ContentId consistency
                var contentIdMatches = 0;
                var contentIdMismatches = new List<string>();
                
                foreach (var row in matchedRows)
                {
                    var targetContentId = row.TargetContentId;
                    
                    if (!string.IsNullOrEmpty(targetContentId) && refContentMap.ContainsKey(targetContentId))
                    {
                        contentIdMatches++;
                    }
                    else
                    {
                        contentIdMismatches.Add($"Target ContentId '{targetContentId}' not found in reference");
                    }
                }
                
                var accuracy = matchedRows.Count > 0 ? (double)contentIdMatches / matchedRows.Count * 100 : 0;
                
                Console.WriteLine($"\n📈 ContentId Consistency Analysis:");
                Console.WriteLine($"   ✅ ContentId matches: {contentIdMatches}/{matchedRows.Count}");
                Console.WriteLine($"   📊 Accuracy: {accuracy:F2}%");
                
                if (contentIdMismatches.Any())
                {
                    Console.WriteLine($"   ❌ Found {contentIdMismatches.Count} ContentId mismatches:");
                    foreach (var mismatch in contentIdMismatches.Take(5))
                    {
                        Console.WriteLine($"      • {mismatch}");
                    }
                    if (contentIdMismatches.Count > 5)
                    {
                        Console.WriteLine($"      • ... and {contentIdMismatches.Count - 5} more");
                    }

                    // Ghi báo cáo mismatch ra file CSV
                    var mismatchFile = "test_output_mismatch.csv";
                    using (var writer = new StreamWriter(mismatchFile, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("Index,RefContentId,RefContent,TargetContentId,TargetContent,SimilarityScore,Status");
                        int idx = 0;
                        foreach (var row in matchedRows)
                        {
                            var targetContentId = row.TargetContentId;
                            if (string.IsNullOrEmpty(targetContentId) || !refContentMap.ContainsKey(targetContentId))
                            {
                                string refContentId = string.Empty;
                                if (row.RefLineNumber != null && row.RefLineNumber.Value > 0 && row.RefLineNumber.Value <= referenceRows.Count)
                                {
                                    refContentId = referenceRows[row.RefLineNumber.Value - 1].ContentId;
                                }
                                var refContent = row.RefContent?.Replace("\"", "''").Replace("\n", " ") ?? "";
                                var targetContent = row.TargetContent?.Replace("\"", "''").Replace("\n", " ") ?? "";
                                writer.WriteLine($"{idx},\"{refContentId}\",\"{refContent}\",\"{targetContentId}\",\"{targetContent}\",{row.SimilarityScore:F3},{row.Status}");
                            }
                            idx++;
                        }
                    }
                    Console.WriteLine($"   📄 Exported mismatch report to {mismatchFile}");
                }
                
                // Show some examples of successful matches
                Console.WriteLine($"\n🎯 Examples of successful matches:");
                var successfulMatches = matchedRows
                    .Where(r => !string.IsNullOrEmpty(r.TargetContentId) && refContentMap.ContainsKey(r.TargetContentId))
                    .Take(3)
                    .ToList();
                    
                foreach (var match in successfulMatches)
                {
                    Console.WriteLine($"   ✅ ContentId: {match.TargetContentId}");
                    Console.WriteLine($"      Ref:    {match.RefContent.Substring(0, Math.Min(50, match.RefContent.Length))}...");
                    Console.WriteLine($"      Target: {match.TargetContent.Substring(0, Math.Min(50, match.TargetContent.Length))}...");
                    Console.WriteLine($"      Score:  {match.SimilarityScore:F3}");
                    Console.WriteLine();
                }
                
                // Summary
                Console.WriteLine("🏁 Test Summary:");
                if (accuracy >= 80)
                {
                    Console.WriteLine("   🎉 PASSED! Alignment algorithm working correctly.");
                    Console.WriteLine($"   ✅ {accuracy:F1}% accuracy meets the 80% threshold");
                }
                else
                {
                    Console.WriteLine("   ❌ FAILED! Alignment algorithm needs improvement.");
                    Console.WriteLine($"   📉 {accuracy:F1}% accuracy below 80% threshold");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Analysis failed: {ex.Message}");
            }
        }
    }
} 