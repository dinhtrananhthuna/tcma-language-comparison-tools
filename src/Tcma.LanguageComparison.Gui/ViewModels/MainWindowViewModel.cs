using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Gui.Services;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

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
        private ObservableCollection<AlignedDisplayRow> results = new();
        partial void OnResultsChanged(ObservableCollection<AlignedDisplayRow> value)
        {
            if (value != null)
            {
                value.CollectionChanged += (s, e) => ExportCommand.NotifyCanExecuteChanged();
            }
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
                StatusMessage = "Loading files...";
                Results.Clear();
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

                // Start reference embeddings and translation in parallel
                StatusMessage = "Starting embeddings and translation...";
                var referenceEmbeddingsTask = geminiService.GenerateEmbeddingsAsync(referenceRows);
                var translationTask = translationService.TranslateBatchAsync(targetRows, "auto", "en");

                // Wait for translation to complete first (needed for target embeddings)
                StatusMessage = "Translating target content...";
                var translateResult = await translationTask;
                if (!translateResult.IsSuccess)
                {
                    StatusMessage = "Failed to translate target.";
                    await _errorHandler.HandleErrorAsync(translateResult.Error!);
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
                preprocessingService.ProcessContentRows(targetRows);

                // Now start target embeddings in parallel with waiting for reference embeddings
                StatusMessage = "Generating embeddings for both reference and target...";
                var targetEmbeddingsTask = geminiService.GenerateEmbeddingsAsync(targetRows);

                // Wait for reference embeddings
                var referenceEmbeddingsResult = await referenceEmbeddingsTask;
                if (!referenceEmbeddingsResult.IsSuccess) throw new Exception(referenceEmbeddingsResult.Error!.UserMessage);

                // Wait for target embeddings
                var targetEmbeddingsResult = await targetEmbeddingsTask;
                if (!targetEmbeddingsResult.IsSuccess) throw new Exception(targetEmbeddingsResult.Error!.UserMessage);

                StatusMessage = "Matching content...";
                var matchingService = new Core.Services.ContentMatchingService(_settingsService.SimilarityThreshold);
                var alignedDisplayData = await matchingService.GenerateAlignedDisplayDataAsync(referenceRows, targetRows, originalTargetRows, translated);

                Results.Clear();
                foreach (var displayRow in alignedDisplayData)
                {
                    Results.Add(displayRow);
                }
                StatusMessage = "Comparison completed successfully.";
            }
            catch (Exception ex)
            {
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