using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Gui.Services;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Tcma.LanguageComparison.Core.Services;

namespace Tcma.LanguageComparison.Gui.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IErrorHandlingService _errorHandler;
        private readonly IRetryPolicyService _retryPolicyService;

        [ObservableProperty]
        private string? referenceFilePath;
        [ObservableProperty]
        private string? targetFilePath;
        [ObservableProperty]
        private ObservableCollection<AlignedDisplayRow>? results = new();
        private ObservableCollection<AlignedDisplayRow>? _oldResults;
        
        [ObservableProperty]
        private bool isDataGridEnabled = true;
        partial void OnResultsChanged(ObservableCollection<AlignedDisplayRow>? value)
        {
            if (_oldResults != null)
            {
                _oldResults.CollectionChanged -= Results_CollectionChanged;
            }
            if (value != null)
            {
                value.CollectionChanged += Results_CollectionChanged;
            }
            _oldResults = value;
            ExportCommand.NotifyCanExecuteChanged();
        }
        private void Results_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ExportCommand.NotifyCanExecuteChanged();
        }
        [ObservableProperty]
        private List<LineByLineMatchResult>? comparisonResults;
        [ObservableProperty]
        private string statusMessage = "Ready";

        public MainWindowViewModel(
            ISettingsService settingsService,
            IErrorHandlingService errorHandler,
            IRetryPolicyService retryPolicyService)
        {
            _settingsService = settingsService;
            _errorHandler = errorHandler;
            _retryPolicyService = retryPolicyService;
            // Tự động load settings khi khởi tạo ViewModel
            Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();

            // Theo dõi thay đổi collection Results để cập nhật trạng thái ExportCommand
            Results.CollectionChanged += (s, e) => ExportCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void BrowseReference()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Reference CSV File",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FilterIndex = 1
            };
            if (openFileDialog.ShowDialog() == true)
            {
                ReferenceFilePath = openFileDialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseTarget()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Target CSV File",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FilterIndex = 1
            };
            if (openFileDialog.ShowDialog() == true)
            {
                TargetFilePath = openFileDialog.FileName;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private async Task ExportAsync()
        {
            if (Results == null || Results.Count == 0)
            {
                await _errorHandler.HandleErrorAsync(new Core.Models.ErrorInfo
                {
                    Category = Core.Models.ErrorCategory.UserInput,
                    Severity = Core.Models.ErrorSeverity.High,
                    UserMessage = "No comparison results to export. Please run comparison first."
                });
                return;
            }
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
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
                    var csvService = new Core.Services.CsvReaderService();
                    var exportResult = await csvService.ExportAlignedDisplayRowsAsync(saveFileDialog.FileName, Results.ToList());
                    if (exportResult.IsSuccess)
                    {
                        MessageBox.Show("Export completed successfully!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        await _errorHandler.HandleErrorAsync(exportResult.Error ?? new Core.Models.ErrorInfo
                        {
                            Category = Core.Models.ErrorCategory.UnexpectedError,
                            Severity = Core.Models.ErrorSeverity.High,
                            UserMessage = "Export failed"
                        });
                    }
                }
                catch (Exception ex)
                {
                    var error = _errorHandler.ProcessException(ex, "Exporting aligned target file");
                    await _errorHandler.HandleErrorAsync(error);
                }
            }
        }

        private bool CanExport()
        {
            return Results != null && Results.Count > 0;
        }

        [RelayCommand(CanExecute = nameof(CanCompare))]
        private async Task CompareAsync()
        {
            StatusMessage = "Starting comparison...";
            if (string.IsNullOrEmpty(ReferenceFilePath) || string.IsNullOrEmpty(TargetFilePath))
            {
                StatusMessage = "Please select both reference and target files.";
                await _errorHandler.HandleErrorAsync(new Core.Models.ErrorInfo
                {
                    Category = Core.Models.ErrorCategory.UserInput,
                    Severity = Core.Models.ErrorSeverity.High,
                    UserMessage = StatusMessage
                });
                return;
            }
            if (string.IsNullOrEmpty(_settingsService.ApiKey))
            {
                StatusMessage = "Please configure your API key in Settings.";
                await _errorHandler.HandleErrorAsync(new Core.Models.ErrorInfo
                {
                    Category = Core.Models.ErrorCategory.Configuration,
                    Severity = Core.Models.ErrorSeverity.High,
                    UserMessage = StatusMessage
                });
                return;
            }
            try
            {
                // Clear results at start of comparison
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Results = null; // Disconnect collection for clean start
                });

                StatusMessage = "Loading files...";
                var csvReader = new Core.Services.CsvReaderService();
                var configService = new Core.Services.ConfigurationService();
                var geminiService = new Core.Services.GeminiEmbeddingService(_settingsService.ApiKey, _settingsService.MaxEmbeddingBatchSize);
                var preprocessingService = new Core.Services.TextPreprocessingService();
                var translationService = new Core.Services.GeminiTranslationService(_settingsService.ApiKey);

                StatusMessage = "Testing API connection...";
                var testResult = await geminiService.TestConnectionAsync();
                if (!testResult.IsSuccess)
                {
                    StatusMessage = "Cannot connect to Gemini API.";
                    await _errorHandler.HandleErrorAsync(testResult.Error!);
                    return;
                }

                StatusMessage = "Reading reference file...";
                var referenceResult = await csvReader.ReadContentRowsAsync(ReferenceFilePath);
                if (!referenceResult.IsSuccess) throw new Exception(referenceResult.Error!.UserMessage);
                var referenceRows = referenceResult.Data!;

                StatusMessage = "Reading target file...";
                var targetResult = await csvReader.ReadContentRowsAsync(TargetFilePath);
                if (!targetResult.IsSuccess) throw new Exception(targetResult.Error!.UserMessage);
                var targetRows = targetResult.Data!;
                var originalTargetRows = targetRows.ToList();

                StatusMessage = "Preprocessing content...";
                preprocessingService.ProcessContentRows(referenceRows);
                preprocessingService.ProcessContentRows(targetRows);

                // Reset embedding service state for new comparison
                geminiService.ResetAdaptiveState();

                // Start reference embeddings and translation in parallel
                StatusMessage = "Starting embeddings and translation...";
                Task<OperationResult<EmbeddingProcessingStats>>? referenceEmbeddingsTask = null;
                Task<OperationResult<List<TranslationResult>>>? translationTask = null;
                Task<OperationResult<EmbeddingProcessingStats>>? targetEmbeddingsTask = null;
                List<TranslationResult> translated = new List<TranslationResult>();

                try 
                {
                    referenceEmbeddingsTask = geminiService.GenerateEmbeddingsAsync(referenceRows);
                    translationTask = translationService.TranslateBatchAsync(targetRows, "auto", "en");

                    // Wait for translation to complete first (needed for target embeddings)
                    StatusMessage = "Translating target content...";
                    var translateResult = await translationTask;
                    translationTask = null; // Mark as completed
                    
                    if (!translateResult.IsSuccess)
                    {
                        StatusMessage = "Failed to translate target.";
                        await _errorHandler.HandleErrorAsync(translateResult.Error!);
                        return;
                    }
                    translated = translateResult.Data!;
                    var translatedDict = translated.ToDictionary(t => t.ContentId, t => t.TranslatedContent);
                    for (int i = 0; i < targetRows.Count; i++)
                    {
                        var row = targetRows[i];
                        if (translatedDict.TryGetValue(row.ContentId, out var trans))
                        {
                            targetRows[i] = row with { Content = trans };
                        }
                    }
                    preprocessingService.ProcessContentRows(targetRows);

                    // Now start target embeddings in parallel with waiting for reference embeddings
                    StatusMessage = "Generating embeddings for both reference and target...";
                    targetEmbeddingsTask = geminiService.GenerateEmbeddingsAsync(targetRows);

                    // Wait for reference embeddings
                    var referenceEmbeddingsResult = await referenceEmbeddingsTask;
                    referenceEmbeddingsTask = null; // Mark as completed
                    if (!referenceEmbeddingsResult.IsSuccess) throw new Exception(referenceEmbeddingsResult.Error!.UserMessage);

                    // Wait for target embeddings
                    var targetEmbeddingsResult = await targetEmbeddingsTask;
                    targetEmbeddingsTask = null; // Mark as completed
                    if (!targetEmbeddingsResult.IsSuccess) throw new Exception(targetEmbeddingsResult.Error!.UserMessage);
                }
                catch (Exception)
                {
                    // Cancel any remaining tasks
                    if (referenceEmbeddingsTask != null && !referenceEmbeddingsTask.IsCompleted)
                    {
                        try { await referenceEmbeddingsTask; } catch { }
                    }
                    if (translationTask != null && !translationTask.IsCompleted)
                    {
                        try { await translationTask; } catch { }
                    }
                    if (targetEmbeddingsTask != null && !targetEmbeddingsTask.IsCompleted)
                    {
                        try { await targetEmbeddingsTask; } catch { }
                    }
                    throw;
                }

                StatusMessage = "Matching content...";
                var matchingService = new Core.Services.ContentMatchingService(_settingsService.SimilarityThreshold);
                // Ensure clean state for new comparison (cache is already cleared by new instance)
                var alignedDisplayData = await matchingService.GenerateAlignedDisplayDataAsync(referenceRows, targetRows, originalTargetRows, translated);

                // Create new collection OFF the UI thread to avoid any DataGrid interference
                var newResults = new ObservableCollection<AlignedDisplayRow>();
                foreach (var displayRow in alignedDisplayData)
                {
                    newResults.Add(displayRow);
                }
                
                // Safe collection update: disable DataGrid, update collection, re-enable
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsDataGridEnabled = false; // Prevent virtualization issues
                });
                
                await Task.Delay(50); // Allow DataGrid to disable
                
                try
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Results = newResults; // Update with new data
                    });
                }
                finally
                {
                    // Always re-enable DataGrid, even if update fails
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsDataGridEnabled = true;
                    });
                    await Task.Delay(50); // Allow DataGrid to re-initialize
                }
                
                StatusMessage = "Comparison completed successfully.";
            }
            catch (Exception ex)
            {
                // Ensure DataGrid is re-enabled even if comparison fails
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsDataGridEnabled = true;
                });
                
                StatusMessage = $"Error: {ex.Message}";
                var error = _errorHandler.ProcessException(ex, "Comparing files");
                await _errorHandler.HandleErrorAsync(error);
            }
        }

        private bool CanCompare()
        {
            return !string.IsNullOrEmpty(ReferenceFilePath)
                && !string.IsNullOrEmpty(TargetFilePath)
                && !string.IsNullOrEmpty(_settingsService.ApiKey);
        }

        partial void OnReferenceFilePathChanged(string? value)
        {
            CompareCommand.NotifyCanExecuteChanged();
        }
        partial void OnTargetFilePathChanged(string? value)
        {
            CompareCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void Settings()
        {
            // Lấy SettingsService thực tế từ DI container
            var settingsService = (SettingsService)App.ServiceProvider!.GetRequiredService<SettingsService>();
            var settingsWindow = new Gui.SettingsWindow(settingsService);
            settingsWindow.Owner = Application.Current.MainWindow;
            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
            {
                // Reload settings sau khi lưu
                Task.Run(async () => await _settingsService.LoadSettingsAsync()).Wait();
                // Notify command can execute changed
                CompareCommand.NotifyCanExecuteChanged();
            }
        }
    }
} 