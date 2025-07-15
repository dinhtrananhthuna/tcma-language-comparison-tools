using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
    private string? _referenceFilePath;
    private string? _targetFilePath;
    private List<LineByLineMatchResult>? _comparisonResults;
    private readonly SettingsService _settingsService;
    private readonly ErrorHandlingService _errorHandler;
    
    public ObservableCollection<ComparisonResultViewModel> Results { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
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
            ReferenceFileTextBox.Text = _referenceFilePath;
            UpdateCompareButtonState();
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
            TargetFileTextBox.Text = _targetFilePath;
            UpdateCompareButtonState();
        }
    }

    private void UpdateCompareButtonState()
    {
        var apiKey = _settingsService.ApiKey;
        CompareButton.IsEnabled = !string.IsNullOrEmpty(_referenceFilePath) 
                                 && !string.IsNullOrEmpty(_targetFilePath) 
                                 && !string.IsNullOrEmpty(apiKey);
    }

    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_referenceFilePath) || string.IsNullOrEmpty(_targetFilePath))
        {
            var error = new ErrorInfo
            {
                Category = ErrorCategory.UserInput,
                Severity = ErrorSeverity.Medium,
                UserMessage = "Vui lòng chọn cả file reference và target CSV.",
                SuggestedAction = "Sử dụng nút Browse để chọn các file cần thiết."
            };
            await _errorHandler.HandleErrorAsync(error);
            return;
        }

        var apiKey = _settingsService.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            var error = new ErrorInfo
            {
                Category = ErrorCategory.Configuration,
                Severity = ErrorSeverity.High,
                UserMessage = "Vui lòng cấu hình Gemini API key trong Settings.",
                SuggestedAction = "Nhấn nút Settings để nhập API key."
            };
            await _errorHandler.HandleErrorAsync(error);
            return;
        }

        // Test network connectivity first
        if (!await _errorHandler.TestNetworkConnectivityAsync())
        {
            var error = CommonErrors.NetworkConnectionError();
            await _errorHandler.HandleErrorAsync(error);
            return;
        }

        try
        {
            // Disable UI during processing
            SetUIEnabled(false);
            ShowProgress("Starting comparison...");
            Results.Clear();

            // Initialize services
            var csvReader = new CsvReaderService();
            var textPreprocessor = new TextPreprocessingService();
            var embeddingService = new GeminiEmbeddingService(apiKey);
            var contentMatcher = new ContentMatchingService(_settingsService.SimilarityThreshold);

            // Test API connection first
            ShowProgress("Testing API connection...");
            var connectionResult = await embeddingService.TestConnectionAsync();
            if (!connectionResult.IsSuccess)
            {
                await _errorHandler.HandleErrorAsync(connectionResult.Error!);
                return;
            }

            // Load CSV files with error handling
            ShowProgress("Loading reference file...");
            var refResult = await csvReader.ReadContentRowsAsync(_referenceFilePath);
            var referenceRows = await _errorHandler.HandleResultAsync(refResult);
            if (referenceRows == null) return;
            
            ShowProgress("Loading target file...");
            var targetResult = await csvReader.ReadContentRowsAsync(_targetFilePath);
            var targetRows = await _errorHandler.HandleResultAsync(targetResult);
            if (targetRows == null) return;

            ShowProgress($"Processing {referenceRows.Count} reference rows and {targetRows.Count} target rows...");

            // Preprocess content
            ShowProgress("Preprocessing content...");
            textPreprocessor.ProcessContentRows(referenceRows);
            textPreprocessor.ProcessContentRows(targetRows);

            // Create progress reporter
            var progress = new Progress<string>(message => 
            {
                Dispatcher.Invoke(() => ShowProgress(message));
            });

            // Generate embeddings with error handling
            ShowProgress("Generating embeddings for reference content...");
            var refEmbeddingResult = await embeddingService.GenerateEmbeddingsAsync(referenceRows, progress);
            var refStats = await _errorHandler.HandleResultAsync(refEmbeddingResult);
            if (refStats == null) return;

            ShowProgress("Generating embeddings for target content...");
            var targetEmbeddingResult = await embeddingService.GenerateEmbeddingsAsync(targetRows, progress);
            var targetStats = await _errorHandler.HandleResultAsync(targetEmbeddingResult);
            if (targetStats == null) return;

            // Check if we have enough successful embeddings
            if (refStats.SuccessfulRows == 0 || targetStats.SuccessfulRows == 0)
            {
                var error = new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Không thể tạo embeddings cho dữ liệu.",
                    TechnicalDetails = $"Reference: {refStats.SuccessfulRows}/{refStats.TotalRows}, Target: {targetStats.SuccessfulRows}/{targetStats.TotalRows}",
                    SuggestedAction = "Vui lòng kiểm tra dữ liệu và API key."
                };
                await _errorHandler.HandleErrorAsync(error);
                return;
            }

            // Perform matching with error handling
            ShowProgress("Finding matches...");
            var matchingResult = await contentMatcher.GenerateLineByLineReportAsync(
                referenceRows, targetRows, progress);
            _comparisonResults = await _errorHandler.HandleResultAsync(matchingResult);
            if (_comparisonResults == null) return;

            // Update UI with results
            ShowProgress("Updating results...");
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (var result in _comparisonResults)
                    {
                        Results.Add(new ComparisonResultViewModel(result));
                    }
                });
            });

            // Generate statistics
            var stats = contentMatcher.GetMatchingStatistics(_comparisonResults.Select(r => new MatchResult
            {
                ReferenceRow = r.CorrespondingReferenceRow ?? new ContentRow(),
                MatchedRow = r.TargetRow,
                SimilarityScore = r.LineByLineScore,
                IsGoodMatch = r.IsGoodLineByLineMatch
            }));
            UpdateStatistics(stats);

            ShowProgress("Comparison completed successfully.");
            ExportButton.IsEnabled = true;

            // Show completion message with detailed statistics
            var completionMessage = $"So sánh hoàn thành thành công!\n\n" +
                                  $"Tổng số reference rows: {stats.TotalReferenceRows}\n" +
                                  $"Matches tốt: {stats.GoodMatches}\n" +
                                  $"Chất lượng cao: {stats.HighQualityMatches}\n" +
                                  $"Chất lượng trung bình: {stats.MediumQualityMatches}\n" +
                                  $"Chất lượng thấp: {stats.LowQualityMatches}\n" +
                                  $"Tỷ lệ match: {stats.MatchPercentage:F1}%\n\n" +
                                  $"Embedding stats:\n" +
                                  $"Reference: {refStats.SuccessfulRows}/{refStats.TotalRows} ({refStats.SuccessRate:F1}%)\n" +
                                  $"Target: {targetStats.SuccessfulRows}/{targetStats.TotalRows} ({targetStats.SuccessRate:F1}%)";

            MessageBox.Show(completionMessage, "Comparison Complete", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var error = _errorHandler.ProcessException(ex, "Performing comparison");
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
        if (_comparisonResults == null || !_comparisonResults.Any())
        {
            var error = new ErrorInfo
            {
                Category = ErrorCategory.UserInput,
                Severity = ErrorSeverity.Medium,
                UserMessage = "Không có kết quả so sánh để export.",
                SuggestedAction = "Vui lòng thực hiện so sánh trước khi export."
            };
            await _errorHandler.HandleErrorAsync(error);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Save Report CSV File",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1,
            FileName = "report_" + Path.GetFileName(_targetFilePath)
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                SetUIEnabled(false);
                ShowProgress("Exporting report...");

                var csvReader = new CsvReaderService();
                var exportData = _comparisonResults.Select(result => new ContentRow
                {
                    ContentId = $"Line{result.TargetRow.OriginalIndex + 1}",
                    Content = CreateExportContent(result)
                }).ToList();

                var exportResult = await csvReader.WriteContentRowsAsync(saveFileDialog.FileName, exportData);
                
                if (exportResult.IsSuccess)
                {
                    ShowStatus($"Report exported successfully to: {saveFileDialog.FileName}");
                    MessageBox.Show($"Report exported successfully!\n\nFile: {saveFileDialog.FileName}", 
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    await _errorHandler.HandleErrorAsync(exportResult.Error!);
                }
            }
            catch (Exception ex)
            {
                var error = _errorHandler.ProcessException(ex, "Exporting report");
                await _errorHandler.HandleErrorAsync(error);
            }
            finally
            {
                SetUIEnabled(true);
                HideProgress();
            }
        }
    }

    private string CreateExportContent(LineByLineMatchResult result)
    {
        var content = $"Target Line #{result.TargetRow.OriginalIndex + 1}: {result.TargetRow.Content}|||";
        content += $"Reference Content: {result.CorrespondingReferenceRow?.Content ?? "N/A"}|||";
        content += $"Similarity Score: {result.LineByLineScore:F3}|||";
        content += $"Quality: {result.Quality}|||";
        
        if (result.SuggestedMatch != null && !result.IsGoodLineByLineMatch)
        {
            content += $"Suggestion: {result.SuggestedMatch.ReferenceRow.Content} (Score: {result.SuggestedMatch.SimilarityScore:F3})";
        }
        else
        {
            content += "Suggestion: N/A";
        }
        
        return content;
    }

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

    private void UpdateStatistics(MatchingStatistics stats)
    {
        StatisticsTextBlock.Text = $"Results: {stats.TotalReferenceRows} total | " +
                                  $"Good matches: {stats.GoodMatches} | " +
                                  $"High: {stats.HighQualityMatches} | " +
                                  $"Medium: {stats.MediumQualityMatches} | " +
                                  $"Low: {stats.LowQualityMatches} | " +
                                  $"Match rate: {stats.MatchPercentage:F1}%";
    }
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