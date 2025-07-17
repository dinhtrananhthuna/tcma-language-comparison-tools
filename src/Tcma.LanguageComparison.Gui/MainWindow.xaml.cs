using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Core.Services;
using Tcma.LanguageComparison.Gui.Services;

namespace Tcma.LanguageComparison.Gui;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Single file mode properties
    private string? _referenceFilePath;
    private string? _targetFilePath;
    private List<LineByLineMatchResult>? _comparisonResults;
    
    // Services
    private readonly SettingsService _settingsService;
    private readonly ErrorHandlingService _errorHandler;
    
    // UI Collections
    public ObservableCollection<AlignedDisplayRow> Results { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Bind UI collections
        ResultsDataGrid.ItemsSource = Results;
        
        _settingsService = new SettingsService();
        
        // Initialize error handling service with UI callbacks
        _errorHandler = new ErrorHandlingService(
            statusUpdater: ShowStatus,
            progressUpdater: ShowProgress,
            hideProgress: HideProgress
        );
        
        // Load settings and initialize UI
        Task.Run(InitializeAsync);
        
        // Handle window closing
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Clean up resources
    }

    private async Task InitializeAsync()
    {
        var result = await _settingsService.LoadSettingsAsync();
        if (result.IsSuccess)
        {
            ShowStatus("Settings loaded successfully");
        }
        else
        {
            await _errorHandler.HandleErrorAsync(result.Error!, showDialog: false);
        }
    }

    #region Single File Mode Event Handlers

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new SettingsWindow(_settingsService)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
            {
                ShowStatus("Settings saved successfully");
            }
        }
        catch (Exception ex)
        {
            var error = _errorHandler.ProcessException(ex, "Opening settings window");
            await _errorHandler.HandleErrorAsync(error);
        }
    }

    private void BrowseReferenceButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Reference CSV File",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _referenceFilePath = openFileDialog.FileName;
            ReferenceFileTextBox.Text = Path.GetFileName(_referenceFilePath);
            SetUIEnabled(true);
        }
    }

    private void BrowseTargetButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Target CSV File",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _targetFilePath = openFileDialog.FileName;
            TargetFileTextBox.Text = Path.GetFileName(_targetFilePath);
            SetUIEnabled(true);
        }
    }

    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_referenceFilePath) || string.IsNullOrEmpty(_targetFilePath))
        {
            ShowError("Please select both reference and target files.");
            return;
        }

        if (string.IsNullOrEmpty(_settingsService.ApiKey))
        {
            ShowError("Please configure your API key in Settings.");
            return;
        }

        try
        {
            SetUIEnabled(false);
            ShowProgress("Starting comparison...");
            Results.Clear();

            // Initialize services
            var csvReader = new CsvReaderService();
            var configService = new ConfigurationService();
            var geminiService = new GeminiEmbeddingService(_settingsService.ApiKey, _settingsService.MaxEmbeddingBatchSize);
            var preprocessingService = new TextPreprocessingService();
            var translationService = new GeminiTranslationService(_settingsService.ApiKey);

            ShowProgress("Testing API connection...");
            var testResult = await geminiService.TestConnectionAsync();
            if (!testResult.IsSuccess)
            {
                await _errorHandler.HandleErrorAsync(testResult.Error!);
                return;
            }

            ShowProgress("Loading reference file...");
            var referenceResult = await csvReader.ReadContentRowsAsync(_referenceFilePath);
            if (!referenceResult.IsSuccess) throw new Exception(referenceResult.Error!.UserMessage);
            var referenceRows = referenceResult.Data!;

            ShowProgress("Loading target file...");
            var targetResult = await csvReader.ReadContentRowsAsync(_targetFilePath);
            if (!targetResult.IsSuccess) throw new Exception(targetResult.Error!.UserMessage);
            var targetRows = targetResult.Data!;
            
            // Lưu trữ nội dung gốc trước khi dịch
            var originalTargetRows = targetRows.ToList();

            ShowProgress($"Processing {referenceRows.Count} reference rows and {targetRows.Count} target rows...");

            // Preprocess content
            ShowProgress("Preprocessing content...");
            preprocessingService.ProcessContentRows(referenceRows);
            preprocessingService.ProcessContentRows(targetRows);

            // Progress callback for embedding generation
            var progress = new Progress<string>(message => 
            {
                Dispatcher.Invoke(() => ShowProgress(message));
            });

            ShowProgress("Generating embeddings for reference content...");
            var referenceEmbeddingsResult = await geminiService.GenerateEmbeddingsAsync(referenceRows, progress);
            if (!referenceEmbeddingsResult.IsSuccess) throw new Exception(referenceEmbeddingsResult.Error!.UserMessage);

            ShowProgress("Translating target content to English using Gemini Flash...");
            var translationProgress = new Progress<string>(msg => Dispatcher.Invoke(() => ShowProgress($"[Translate] {msg}")));
            var translateResult = await translationService.TranslateBatchAsync(targetRows, "auto", "en", translationProgress);
            if (!translateResult.IsSuccess)
            {
                ShowError($"Failed to translate target: {translateResult.Error?.UserMessage}");
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
            ShowProgress("Translation completed. Proceeding to preprocessing and embedding...");

            ShowProgress("Generating embeddings for target content...");
            
            // Re-preprocess target content sau khi translate để update CleanContent
            ShowProgress("Re-preprocessing translated target content...");
            var textPreprocessor = new TextPreprocessingService();
            textPreprocessor.ProcessContentRows(targetRows);
            
            // OPTIMIZATION: Reuse embeddings cho cùng content để tránh API inconsistency
            ShowProgress("Optimizing embeddings - reusing for identical content...");
            
            // Tạo content embedding cache từ reference 
            var contentEmbeddingCache = new Dictionary<string, float[]>();
            foreach (var refRow in referenceRows.Where(r => r.EmbeddingVector != null))
            {
                var cleanContent = refRow.CleanContent?.Trim();
                if (!string.IsNullOrEmpty(cleanContent) && !contentEmbeddingCache.ContainsKey(cleanContent))
                {
                    contentEmbeddingCache[cleanContent] = refRow.EmbeddingVector!;
                }
            }
            
            // Apply cached embeddings cho target rows có cùng content
            int reusedCount = 0;
            for (int i = 0; i < targetRows.Count; i++)
            {
                var targetRow = targetRows[i];
                var cleanContent = targetRow.CleanContent?.Trim();
                if (!string.IsNullOrEmpty(cleanContent) && contentEmbeddingCache.TryGetValue(cleanContent, out var cachedEmbedding))
                {
                    targetRows[i] = targetRow with { EmbeddingVector = cachedEmbedding };
                    reusedCount++;
                }
            }
            
            ShowProgress($"Reused {reusedCount} embeddings. Generating remaining embeddings...");
            
            var targetEmbeddingsResult = await geminiService.GenerateEmbeddingsAsync(targetRows, progress);
            if (!targetEmbeddingsResult.IsSuccess) throw new Exception(targetEmbeddingsResult.Error!.UserMessage);

            ShowProgress("Finding optimal matches...");
            var matchingService = new ContentMatchingService(_settingsService.SimilarityThreshold);
            
            // Sử dụng thuật toán optimal matching như unit test thay vì line-by-line
            var alignedDisplayData = await matchingService.GenerateAlignedDisplayDataAsync(referenceRows, targetRows, originalTargetRows, translated, progress);
            
            // Tạo _comparisonResults từ aligned data để tương thích với export logic
            _comparisonResults = alignedDisplayData.Select(row => new LineByLineMatchResult
            {
                TargetRow = new ContentRow { ContentId = row.TargetContentId, Content = row.TargetContent, OriginalIndex = (row.TargetLineNumber ?? 1) - 1 },
                CorrespondingReferenceRow = string.IsNullOrEmpty(row.RefContent) ? null : new ContentRow { Content = row.RefContent, OriginalIndex = (row.RefLineNumber ?? 1) - 1 },
                LineByLineScore = row.SimilarityScore ?? 0.0,
                IsGoodLineByLineMatch = row.Status == "Matched"
            }).ToList();

            ShowProgress("Updating results...");
            
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var displayRow in alignedDisplayData)
                {
                    Results.Add(displayRow);
                }
            });

            // Update statistics
            var referenceAlignedRows = alignedDisplayData.Where(r => r.RowType == AlignedRowType.ReferenceAligned).ToList();
            var unmatchedTargetRows = alignedDisplayData.Where(r => r.RowType == AlignedRowType.UnmatchedTarget).ToList();
            
            var stats = new MatchingStatistics
            {
                TotalReferenceRows = referenceRows.Count,
                GoodMatches = referenceAlignedRows.Count(r => r.Status == "Matched"),
                HighQualityMatches = referenceAlignedRows.Count(r => r.Quality == MatchQuality.High),
                MediumQualityMatches = referenceAlignedRows.Count(r => r.Quality == MatchQuality.Medium),
                LowQualityMatches = referenceAlignedRows.Count(r => r.Quality == MatchQuality.Low),
                PoorQualityMatches = referenceAlignedRows.Count(r => r.Quality == MatchQuality.Poor),
                MatchPercentage = referenceRows.Count > 0 ? (double)referenceAlignedRows.Count(r => r.Status == "Matched") / referenceRows.Count * 100 : 0,
                AverageSimilarityScore = referenceAlignedRows.Where(r => r.SimilarityScore.HasValue).Any() ? 
                    referenceAlignedRows.Where(r => r.SimilarityScore.HasValue).Average(r => r.SimilarityScore!.Value) : 0
            };

            // Update statistics display with unmatched target info
            UpdateStatistics(stats, unmatchedTargetRows.Count, targetRows.Count);
            ExportButton.IsEnabled = true;

            ShowProgress("Comparison completed successfully.");
        }
        catch (Exception ex)
        {
            var error = _errorHandler.ProcessException(ex, "Comparing files");
            await _errorHandler.HandleErrorAsync(error);
        }
        finally
        {
            SetUIEnabled(true);
            HideProgress();
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (Results == null || !Results.Any())
        {
            ShowError("No comparison results to export. Please run comparison first.");
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Export Aligned Target File",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1,
            FileName = $"aligned_target_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                SetUIEnabled(false);
                ShowProgress("Exporting aligned target file...");

                // Sử dụng aligned display data từ DataGrid (đã có sẵn)
                var alignedDisplayData = Results.ToList();

                var csvService = new CsvReaderService();
                var exportResult = await csvService.ExportAlignedDisplayRowsAsync(saveFileDialog.FileName, alignedDisplayData);

                if (exportResult.IsSuccess)
                {
                    ShowStatus($"Aligned target file exported successfully to: {saveFileDialog.FileName}");
                }
                else
                {
                    ShowError(exportResult.Error?.UserMessage ?? "Export failed");
                }
            }
            catch (Exception ex)
            {
                var error = _errorHandler.ProcessException(ex, "Exporting aligned target file");
                await _errorHandler.HandleErrorAsync(error);
            }
            finally
            {
                SetUIEnabled(true);
                HideProgress();
            }
        }
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "\"\"";

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }

    #endregion

    #region UI Helper Methods

    private void SetUIEnabled(bool enabled)
    {
        BrowseReferenceButton.IsEnabled = enabled;
        BrowseTargetButton.IsEnabled = enabled;
        CompareButton.IsEnabled = enabled && !string.IsNullOrEmpty(_referenceFilePath) 
                                           && !string.IsNullOrEmpty(_targetFilePath) 
                                           && !string.IsNullOrEmpty(_settingsService.ApiKey);
    }

    private void ShowProgress(string message)
    {
        ProgressBar.Visibility = Visibility.Visible;
        StatusTextBlock.Text = message;
    }

    private void HideProgress()
    {
        ProgressBar.Visibility = Visibility.Collapsed;
        StatusTextBlock.Text = "Ready";
    }

    private void ShowStatus(string message)
    {
        StatusTextBlock.Text = message;
    }

    private void ShowError(string message)
    {
        StatusTextBlock.Text = $"Error: {message}";
        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void UpdateStatistics(MatchingStatistics stats, int unmatchedTargetCount = 0, int totalTargetRows = 0)
    {
        var baseStats = $"Results: {stats.TotalReferenceRows} ref rows | " +
                       $"Good matches: {stats.GoodMatches} | " +
                       $"High: {stats.HighQualityMatches} | " +
                       $"Medium: {stats.MediumQualityMatches} | " +
                       $"Low: {stats.LowQualityMatches} | " +
                       $"Match rate: {stats.MatchPercentage:F1}%";
        
        var targetStats = unmatchedTargetCount > 0 
            ? $" | Target: {totalTargetRows} total, {unmatchedTargetCount} unmatched"
            : "";
            
        StatisticsTextBlock.Text = baseStats + targetStats;
    }

    #endregion
}

/// <summary>
/// ViewModel for displaying comparison results in DataGrid
/// </summary>
public class ComparisonResultViewModel
{
    public int TargetLineNumber { get; set; }
    public string TargetContent { get; set; } = string.Empty;
    public int ReferenceLineNumber { get; set; }
    public string ReferenceContent { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string Quality { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public Brush RowBackground { get; set; } = Brushes.White;

    public ComparisonResultViewModel(LineByLineMatchResult matchResult)
    {
        TargetLineNumber = matchResult.TargetRow.OriginalIndex + 1;
        TargetContent = TruncateText(matchResult.TargetRow.Content, 100);
        ReferenceLineNumber = matchResult.CorrespondingReferenceRow?.OriginalIndex + 1 ?? 0;
        ReferenceContent = matchResult.CorrespondingReferenceRow != null ? TruncateText(matchResult.CorrespondingReferenceRow.Content, 100) : "N/A";
        SimilarityScore = matchResult.LineByLineScore;
        Quality = matchResult.Quality.ToString();
        Suggestion = matchResult.SuggestedMatch != null ? $"Suggested Ref Line: {matchResult.SuggestedMatch.ReferenceRow.OriginalIndex + 1} (Score: {matchResult.SuggestedMatch.SimilarityScore:F3})" : "None";
        
        // Set color based on quality
        RowBackground = matchResult.Quality switch
        {
            MatchQuality.High => Brushes.LightGreen,
            MatchQuality.Medium => Brushes.LightYellow,
            MatchQuality.Low => Brushes.LightPink,
            _ => Brushes.White
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }
}

/// <summary>
/// Helper class for statistics
/// </summary>
public class MatchingStatistics
{
    public int TotalReferenceRows { get; set; }
    public int GoodMatches { get; set; }
    public int HighQualityMatches { get; set; }
    public int MediumQualityMatches { get; set; }
    public int LowQualityMatches { get; set; }
    public int PoorQualityMatches { get; set; }
    public double MatchPercentage { get; set; }
    public double AverageSimilarityScore { get; set; }
}