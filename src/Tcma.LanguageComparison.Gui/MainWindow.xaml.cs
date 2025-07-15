using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    
    // Multi-page mode properties
    private string? _referenceZipPath;
    private string? _targetZipPath;
    private PageManagementService? _pageManagementService;
    
    // Services
    private readonly SettingsService _settingsService;
    private readonly ErrorHandlingService _errorHandler;
    
    // UI Collections
    public ObservableCollection<ComparisonResultViewModel> Results { get; } = new();
    public ObservableCollection<PageInfo> Pages { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Bind UI collections
        ResultsDataGrid.ItemsSource = Results;
        PageListView.ItemsSource = Pages;
        
        _settingsService = new SettingsService();
        
        // Initialize error handling service with UI callbacks
        _errorHandler = new ErrorHandlingService(
            statusUpdater: ShowStatus,
            progressUpdater: ShowProgress,
            hideProgress: HideProgress
        );
        
        // Load settings and initialize UI
        Task.Run(InitializeAsync);
        
        // Initialize UI visibility based on default tab (Single File Mode)
        SingleFileResultsPanel.Visibility = Visibility.Visible;
        
        // Handle window closing
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Clean up resources
        _pageManagementService?.Dispose();
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
            var geminiService = new GeminiEmbeddingService(_settingsService.ApiKey);
            var preprocessingService = new TextPreprocessingService();

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

            ShowProgress("Generating embeddings for target content...");
            var targetEmbeddingsResult = await geminiService.GenerateEmbeddingsAsync(targetRows, progress);
            if (!targetEmbeddingsResult.IsSuccess) throw new Exception(targetEmbeddingsResult.Error!.UserMessage);

            ShowProgress("Finding matches...");
            var matchingService = new ContentMatchingService(_settingsService.SimilarityThreshold);
            var matchingResult = await matchingService.GenerateLineByLineReportAsync(referenceRows, targetRows, progress);

            if (!matchingResult.IsSuccess) throw new Exception(matchingResult.Error!.UserMessage);

            _comparisonResults = matchingResult.Data!;

            ShowProgress("Updating results...");
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var result in _comparisonResults)
                {
                    Results.Add(new ComparisonResultViewModel(result));
                }
            });

            // Update statistics
            var stats = new MatchingStatistics
            {
                TotalReferenceRows = referenceRows.Count,
                GoodMatches = _comparisonResults.Count(r => r.Quality != MatchQuality.Poor),
                HighQualityMatches = _comparisonResults.Count(r => r.Quality == MatchQuality.High),
                MediumQualityMatches = _comparisonResults.Count(r => r.Quality == MatchQuality.Medium),
                LowQualityMatches = _comparisonResults.Count(r => r.Quality == MatchQuality.Low)
            };

            UpdateStatistics(stats);
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
        if (_comparisonResults == null || !_comparisonResults.Any())
        {
            ShowError("No comparison results to export.");
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Export Comparison Report",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FilterIndex = 1,
            FileName = $"comparison_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                SetUIEnabled(false);
                ShowProgress("Exporting report...");

                var csvContent = "Target Line #,Target Content,Reference Line #,Reference Content,Similarity Score,Quality,Suggestion\n";
                
                foreach (var result in _comparisonResults)
                {
                    var targetLineNumber = result.TargetRow.OriginalIndex + 1;
                    var targetContent = EscapeCsvField(result.TargetRow.Content);
                    var referenceLineNumber = result.CorrespondingReferenceRow?.OriginalIndex + 1 ?? 0;
                    var referenceContent = result.CorrespondingReferenceRow != null ? EscapeCsvField(result.CorrespondingReferenceRow.Content) : "N/A";
                    var similarity = result.LineByLineScore.ToString("F3");
                    var quality = result.Quality.ToString();
                    var suggestion = result.SuggestedMatch != null ? $"Suggested Ref Line: {result.SuggestedMatch.ReferenceRow.OriginalIndex + 1} (Score: {result.SuggestedMatch.SimilarityScore:F3})" : "None";

                    csvContent += $"{targetLineNumber},{targetContent},{referenceLineNumber},{referenceContent},{similarity},{quality},{EscapeCsvField(suggestion)}\n";
                }

                await File.WriteAllTextAsync(saveFileDialog.FileName, csvContent);
                ShowStatus($"Report exported successfully to: {saveFileDialog.FileName}");
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

    private void UpdateStatistics(MatchingStatistics stats)
    {
        StatisticsTextBlock.Text = $"Results: {stats.TotalReferenceRows} total | " +
                                  $"Good matches: {stats.GoodMatches} | " +
                                  $"High: {stats.HighQualityMatches} | " +
                                  $"Medium: {stats.MediumQualityMatches} | " +
                                  $"Low: {stats.LowQualityMatches} | " +
                                  $"Match rate: {stats.MatchPercentage:F1}%";
    }

    #endregion

    #region Multi-Page Mode Event Handlers

    private void ProcessingModeTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Adjust layout based on selected tab
        if (ProcessingModeTabControl.SelectedItem == SingleFileTab)
        {
            // Show comparison results for single file mode
            SingleFileResultsPanel.Visibility = Visibility.Visible;
        }
        else if (ProcessingModeTabControl.SelectedItem == MultiPageTab)
        {
            // Hide comparison results for multi-page mode (has its own results panel)
            SingleFileResultsPanel.Visibility = Visibility.Collapsed;
            
            // Initialize page management service when switching to multi-page mode
            if (_pageManagementService == null)
            {
                InitializePageManagementService();
            }
        }
    }

    private void InitializePageManagementService()
    {
        try
        {
            var apiKey = _settingsService.ApiKey;
            var threshold = _settingsService.SimilarityThreshold;
            
            _pageManagementService?.Dispose();
            _pageManagementService = new PageManagementService(threshold, apiKey);
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to initialize page management: {ex.Message}");
        }
    }

    private void BrowseReferenceZipButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Reference ZIP File",
            Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _referenceZipPath = openFileDialog.FileName;
            ReferenceZipTextBox.Text = Path.GetFileName(_referenceZipPath);
            UpdateLoadPagesButtonState();
        }
    }

    private void BrowseTargetZipButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Target ZIP File",
            Filter = "ZIP Files (*.zip)|*.zip|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _targetZipPath = openFileDialog.FileName;
            TargetZipTextBox.Text = Path.GetFileName(_targetZipPath);
            UpdateLoadPagesButtonState();
        }
    }

    private void UpdateLoadPagesButtonState()
    {
        LoadPagesButton.IsEnabled = !string.IsNullOrEmpty(_referenceZipPath) && 
                                   !string.IsNullOrEmpty(_targetZipPath);
    }

    private async void LoadPagesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pageManagementService == null)
        {
            ShowStatus("Page management service not initialized");
            return;
        }

        if (string.IsNullOrEmpty(_referenceZipPath) || string.IsNullOrEmpty(_targetZipPath))
        {
            ShowStatus("Please select both ZIP files");
            return;
        }

        try
        {
            SetUIEnabled(false);
            ShowProgress("Loading pages from ZIP files...");
            Pages.Clear();

            var result = await _pageManagementService.LoadPagesFromZipsAsync(_referenceZipPath, _targetZipPath);
            
            if (result.IsSuccess)
            {
                var extractionResult = result.Data!;
                
                foreach (var page in extractionResult.Pages)
                {
                    Pages.Add(page);
                }

                PagesStatusText.Text = extractionResult.Summary;
                
                if (extractionResult.UnpairedFiles.Any())
                {
                    ShowStatus($"Loaded {extractionResult.Pages.Count} pages. {extractionResult.UnpairedFiles.Count} files couldn't be paired.");
                }
                else
                {
                    ShowStatus($"Successfully loaded {extractionResult.Pages.Count} pages");
                }
            }
            else
            {
                await _errorHandler.HandleErrorAsync(result.Error!);
                PagesStatusText.Text = "Failed to load pages";
            }
        }
        catch (Exception ex)
        {
            var error = _errorHandler.ProcessException(ex, "Loading pages from ZIP files");
            await _errorHandler.HandleErrorAsync(error);
        }
        finally
        {
            SetUIEnabled(true);
            HideProgress();
        }
    }

    private async void ProcessPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pageManagementService == null)
        {
            ShowStatus("Page management service not initialized");
            return;
        }

        var button = (Button)sender;
        var pageInfo = (PageInfo)button.Tag;

        if (!pageInfo.CanProcess)
        {
            ShowStatus($"Page {pageInfo.PageName} cannot be processed in current state");
            return;
        }

        try
        {
            // Update API key if needed
            var apiKey = _settingsService.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                ShowStatus("Please configure API key in Settings");
                return;
            }

            _pageManagementService.InitializeEmbeddingService(apiKey);

            // Process the page
            var progress = new Progress<string>(message => ShowProgress(message));
            var result = await _pageManagementService.ProcessPageAsync(pageInfo, progress);

            if (result.IsSuccess)
            {
                var pageResult = result.Data!;
                
                // Add/Update results tab
                AddOrUpdateResultsTab(pageResult);
                
                ShowStatus($"Successfully processed page: {pageInfo.PageName}");
            }
            else
            {
                await _errorHandler.HandleErrorAsync(result.Error!);
            }
        }
        catch (Exception ex)
        {
            var error = _errorHandler.ProcessException(ex, $"Processing page {pageInfo.PageName}");
            await _errorHandler.HandleErrorAsync(error);
        }
        finally
        {
            HideProgress();
        }
    }

    private void PageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle page selection if needed
        var selectedPage = PageListView.SelectedItem as PageInfo;
        if (selectedPage != null && _pageManagementService?.HasCachedResult(selectedPage.PageName) == true)
        {
            // Switch to the results tab for this page
            SwitchToResultsTab(selectedPage.PageName);
        }
    }

    private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
    {
        _pageManagementService?.ClearCache();
        
        // Update page statuses
        foreach (var page in Pages)
        {
            if (page.Status == PageStatus.Cached || page.Status == PageStatus.Completed)
            {
                page.Status = PageStatus.Ready;
                page.IsResultsCached = false;
            }
        }

        // Clear results tabs except empty tab
        while (PageResultsTabControl.Items.Count > 1)
        {
            PageResultsTabControl.Items.RemoveAt(1);
        }
        
        PageResultsTabControl.SelectedItem = EmptyResultsTab;
        
        ShowStatus("Cache cleared");
    }

    private void RefreshPagesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pageManagementService != null)
        {
            foreach (var page in Pages)
            {
                _pageManagementService.UpdatePageStatus(page);
            }
        }
        
        ShowStatus("Page statuses refreshed");
    }

    private void AddOrUpdateResultsTab(PageMatchingResult pageResult)
    {
        var pageName = pageResult.PageInfo.PageName;
        
        // Check if tab already exists
        TabItem? existingTab = null;
        foreach (TabItem tab in PageResultsTabControl.Items)
        {
            if (tab.Tag?.ToString() == pageName)
            {
                existingTab = tab;
                break;
            }
        }

        if (existingTab == null)
        {
            // Create new tab
            var newTab = new TabItem
            {
                Header = $"📄 {pageName}",
                Tag = pageName
            };

            // Create DataGrid for this page's results wrapped in ScrollViewer
            var dataGrid = CreateResultsDataGrid(pageResult);
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = true,
                Content = dataGrid
            };
            
            // Create container with proper sizing
            var container = new Grid();
            container.Children.Add(scrollViewer);
            newTab.Content = container;

            PageResultsTabControl.Items.Add(newTab);
            PageResultsTabControl.SelectedItem = newTab;
            
            // Hide empty tab if this is the first real tab
            if (PageResultsTabControl.Items.Count == 2 && PageResultsTabControl.Items[0] == EmptyResultsTab)
            {
                EmptyResultsTab.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            // Update existing tab
            var dataGrid = CreateResultsDataGrid(pageResult);
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                CanContentScroll = true,
                Content = dataGrid
            };
            
            // Create container with proper sizing
            var container = new Grid();
            container.Children.Add(scrollViewer);
            existingTab.Content = container;
            PageResultsTabControl.SelectedItem = existingTab;
        }
    }

    private DataGrid CreateResultsDataGrid(PageMatchingResult pageResult)
    {
        var dataGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            EnableRowVirtualization = true,
            EnableColumnVirtualization = true,
            CanUserResizeColumns = true,
            CanUserSortColumns = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Set attached properties for virtualization
        VirtualizingPanel.SetIsVirtualizing(dataGrid, true);
        VirtualizingPanel.SetVirtualizationMode(dataGrid, VirtualizationMode.Recycling);

        // Create columns with consistent widths
        var columns = new[]
        {
            new DataGridTextColumn { Header = "Target Line #", Binding = new System.Windows.Data.Binding("TargetLineNumber"), Width = new DataGridLength(80, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Target Content", Binding = new System.Windows.Data.Binding("TargetContent"), Width = new DataGridLength(350, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Ref. Line #", Binding = new System.Windows.Data.Binding("ReferenceLineNumber"), Width = new DataGridLength(80, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Reference Content", Binding = new System.Windows.Data.Binding("ReferenceContent"), Width = new DataGridLength(350, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Similarity Score", Binding = new System.Windows.Data.Binding("SimilarityScore") { StringFormat = "F3" }, Width = new DataGridLength(120, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Quality", Binding = new System.Windows.Data.Binding("Quality"), Width = new DataGridLength(80, DataGridLengthUnitType.Pixel) },
            new DataGridTextColumn { Header = "Suggestion", Binding = new System.Windows.Data.Binding("Suggestion"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }
        };

        foreach (var column in columns)
        {
            if (column.Header.ToString()!.Contains("Content"))
            {
                column.ElementStyle = new Style(typeof(TextBlock));
                column.ElementStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                column.ElementStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            }
            dataGrid.Columns.Add(column);
        }

        // Set row style for coloring
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.BackgroundProperty, new System.Windows.Data.Binding("RowBackground")));
        dataGrid.RowStyle = rowStyle;

        // Convert results to view models
        var viewModels = pageResult.Results.Select(r => new ComparisonResultViewModel(r)).ToList();
        dataGrid.ItemsSource = viewModels;

        return dataGrid;
    }

    private void SwitchToResultsTab(string pageName)
    {
        foreach (TabItem tab in PageResultsTabControl.Items)
        {
            if (tab.Tag?.ToString() == pageName)
            {
                PageResultsTabControl.SelectedItem = tab;
                break;
            }
        }
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
    public double MatchPercentage => TotalReferenceRows > 0 ? (double)GoodMatches / TotalReferenceRows * 100 : 0;
}