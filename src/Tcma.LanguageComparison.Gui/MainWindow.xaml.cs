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

namespace Tcma.LanguageComparison.Gui;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string? _referenceFilePath;
    private string? _targetFilePath;
    private List<MatchResult>? _comparisonResults;
    
    public ObservableCollection<ComparisonResultViewModel> Results { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ResultsDataGrid.ItemsSource = Results;
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
        CompareButton.IsEnabled = !string.IsNullOrEmpty(_referenceFilePath) 
                                 && !string.IsNullOrEmpty(_targetFilePath) 
                                 && !string.IsNullOrEmpty(ApiKeyPasswordBox.Password);
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdateCompareButtonState();
    }

    private async void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_referenceFilePath) || string.IsNullOrEmpty(_targetFilePath))
        {
            MessageBox.Show("Please select both reference and target CSV files.", "Missing Files", 
                          MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(ApiKeyPasswordBox.Password))
        {
            MessageBox.Show("Please enter your Gemini API key.", "Missing API Key", 
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
            var embeddingService = new GeminiEmbeddingService(ApiKeyPasswordBox.Password);
            var contentMatcher = new ContentMatchingService(0.35); // Use lower threshold for cross-language

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

            // Generate embeddings
            ShowProgress("Generating embeddings for reference content...");
            await embeddingService.GenerateEmbeddingsAsync(referenceRows, null);

            ShowProgress("Generating embeddings for target content...");
            await embeddingService.GenerateEmbeddingsAsync(targetRows, null);

            // Create progress reporter
            var progress = new Progress<string>(message => 
            {
                Dispatcher.Invoke(() => ShowProgress(message));
            });

            // Perform matching
            ShowProgress("Finding matches...");
            _comparisonResults = await contentMatcher.FindMatchesAsync(
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
            var stats = contentMatcher.GetMatchingStatistics(_comparisonResults);
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
            Title = "Save Corrected CSV File",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1,
            FileName = "corrected_" + Path.GetFileName(_targetFilePath)
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                SetUIEnabled(false);
                ShowProgress("Exporting corrected file...");

                var csvReader = new CsvReaderService();
                var contentMatcher = new ContentMatchingService(0.35);
                var reorderedRows = contentMatcher.CreateReorderedTargetList(_comparisonResults);
                
                await csvReader.WriteContentRowsAsync(saveFileDialog.FileName, reorderedRows);

                ShowProgress("Export completed successfully.");
                MessageBox.Show($"Corrected file exported successfully to:\n{saveFileDialog.FileName}", 
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
        ApiKeyPasswordBox.IsEnabled = enabled;
        CompareButton.IsEnabled = enabled && !string.IsNullOrEmpty(_referenceFilePath) 
                                           && !string.IsNullOrEmpty(_targetFilePath) 
                                           && !string.IsNullOrEmpty(ApiKeyPasswordBox.Password);
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

    private void UpdateStatistics(MatchingStatistics stats)
    {
        StatisticsTextBlock.Text = $"Results: {stats.TotalReferenceRows} total | " +
                                  $"Good matches: {stats.GoodMatches} | " +
                                  $"High: {stats.HighQualityMatches} | " +
                                  $"Medium: {stats.MediumQualityMatches} | " +
                                  $"Low: {stats.LowQualityMatches} | " +
                                  $"Match rate: {stats.MatchPercentage:P1}";
    }
}

/// <summary>
/// ViewModel for displaying comparison results in DataGrid
/// </summary>
public class ComparisonResultViewModel
{
    public int ReferenceLineNumber { get; set; }
    public string ReferenceContent { get; set; } = string.Empty;
    public string MatchedContent { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string Quality { get; set; } = string.Empty;
    public Brush RowBackground { get; set; } = Brushes.White;

    public ComparisonResultViewModel(MatchResult matchResult)
    {
        ReferenceLineNumber = matchResult.ReferenceRow.OriginalIndex + 1; // Convert 0-based to 1-based
        ReferenceContent = TruncateText(matchResult.ReferenceRow.Content, 100);
        MatchedContent = matchResult.MatchedRow != null ? TruncateText(matchResult.MatchedRow.Content, 100) : "No match found";
        SimilarityScore = matchResult.SimilarityScore;
        Quality = matchResult.Quality.ToString();
        
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