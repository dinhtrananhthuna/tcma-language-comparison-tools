using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Gui.Services;

public interface ISettingsService
{
    double SimilarityThreshold { get; }
    string ApiKey { get; }
    int MaxEmbeddingBatchSize { get; }
    event EventHandler<UserSettings>? SettingsChanged;
    Task<OperationResult<UserSettings>> LoadSettingsAsync();
    Task SaveSettingsAsync(double threshold, string apiKey, int maxEmbeddingBatchSize);
    UserSettings GetCurrentSettings();
}

public class UserSettings
{
    public double SimilarityThreshold { get; set; } = 0.5;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxEmbeddingBatchSize { get; set; } = 50;
}

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TcmaLanguageComparison",
        "settings.json"
    );

    private UserSettings _settings = new();

    public double SimilarityThreshold => _settings.SimilarityThreshold;
    public string ApiKey => _settings.ApiKey;
    public int MaxEmbeddingBatchSize => _settings.MaxEmbeddingBatchSize;

    public event EventHandler<UserSettings>? SettingsChanged;

    public async Task<OperationResult<UserSettings>> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<UserSettings>(json);
                if (settings != null)
                {
                    _settings = settings;
                }
            }
            return OperationResult<UserSettings>.Success(_settings);
        }
        catch (Exception)
        {
            // If loading fails, keep default settings
            _settings = new UserSettings();
            return OperationResult<UserSettings>.Success(_settings);
        }
    }

    public async Task SaveSettingsAsync(double threshold, string apiKey, int maxEmbeddingBatchSize)
    {
        _settings.SimilarityThreshold = threshold;
        _settings.ApiKey = apiKey;
        _settings.MaxEmbeddingBatchSize = maxEmbeddingBatchSize;

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save to file
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(SettingsFilePath, json);

            // Notify subscribers
            SettingsChanged?.Invoke(this, _settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
        }
    }

    public UserSettings GetCurrentSettings()
    {
        return new UserSettings
        {
            SimilarityThreshold = _settings.SimilarityThreshold,
            ApiKey = _settings.ApiKey,
            MaxEmbeddingBatchSize = _settings.MaxEmbeddingBatchSize
        };
    }
} 