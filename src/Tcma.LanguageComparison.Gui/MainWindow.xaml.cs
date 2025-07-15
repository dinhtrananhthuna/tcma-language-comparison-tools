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
    
    public ObservableCollection<ComparisonResultViewModel> Results { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ResultsDataGrid.ItemsSource = Results;
        
        _settingsService = new SettingsService();
        
        // Load settings and initialize UI
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _settingsService.LoadSettingsAsync();
            
            // Settings loaded successfully
            ShowStatus("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load settings: {ex.Message}");
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
            ShowError($"Failed to open settings: {ex.Message}");
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
            MessageBox.Show("Please select both reference and target CSV files.", "Missing Files", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var apiKey = _settingsService.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            MessageBox.Show("Please configure your Gemini API key in Settings.", "Missing API Key", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var contentMatcher = new ContentMatchingService(_settingsService.SimilarityThreshold); // Use threshold from settings

            // Load CSV files
            ShowProgress("Loading reference file...");
            var referenceRows = await csvReader.ReadContentRowsAsync(_referenceFilePath);
            
            ShowProgress("Loading target file...");
            var targetRows = await csvReader.ReadContentRowsAsync(_targetFilePath);

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

            // Generate embeddings
            ShowProgress("Generating embeddings for reference content...");
            await embeddingService.GenerateEmbeddingsAsync(referenceRows, progress);

            ShowProgress("Generating embeddings for target content...");
            await embeddingService.GenerateEmbeddingsAsync(targetRows, progress);

            // Perform matching
            ShowProgress("Finding matches...");
            _comparisonResults = await contentMatcher.GenerateLineByLineReportAsync(
                referenceRows, targetRows, progress);

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

            MessageBox.Show($"Comparison completed!\n\n" +
                          $"Total reference rows: {stats.TotalReferenceRows}\n" +
                          $"Good matches: {stats.GoodMatches}\n" +
                          $"High quality: {stats.HighQualityMatches}\n" +
                          $"Medium quality: {stats.MediumQualityMatches}\n" +
                          $"Low quality: {stats.LowQualityMatches}\n" +
                          $"Match percentage: {stats.MatchPercentage:P1}", 
                          "Comparison Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowProgress("Error occurred during comparison.");
            MessageBox.Show($"An error occurred during comparison:\n\n{ex.Message}", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("No comparison results to export.", "No Data", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
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
                // Create report rows with additional columns
                var reportRows = _comparisonResults.Select(r => new 
                {
                    TargetId = r.TargetRow.ContentId,
                    TargetContent = r.TargetRow.Content,
                    RefId = r.CorrespondingReferenceRow?.ContentId ?? "N/A",
                    RefContent = r.CorrespondingReferenceRow?.Content ?? "N/A",
                    Score = r.LineByLineScore.ToString("F3"),
                    Quality = r.Quality.ToString(),
                    Suggestion = r.SuggestedMatch != null ? $"Suggested Ref: {r.SuggestedMatch.ReferenceRow.ContentId} with score {r.SuggestedMatch.SimilarityScore:F3}" : "None"
                });

                // Note: CsvHelper can write anonymous types
                await csvReader.WriteContentRowsAsync(saveFileDialog.FileName, reportRows.Select(r => new ContentRow { ContentId = r.TargetId, Content = $"{r.TargetContent},{r.RefId},{r.RefContent},{r.Score},{r.Quality},{r.Suggestion}" }));

                ShowProgress("Export completed successfully.");
                MessageBox.Show($"Report exported successfully to:\n{saveFileDialog.FileName}", 
                              "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowProgress("Error occurred during export.");
                MessageBox.Show($"An error occurred during export:\n\n{ex.Message}", 
                              "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetUIEnabled(true);
                HideProgress();
            }
        }
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