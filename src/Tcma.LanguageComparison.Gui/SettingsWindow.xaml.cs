using System;
using System.Windows;
using System.Windows.Controls;
using Tcma.LanguageComparison.Gui.Services;

namespace Tcma.LanguageComparison.Gui;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private bool _isInitializing = true;

    public bool SettingsChanged { get; private set; }

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LoadCurrentSettings();
        _isInitializing = false;
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.GetCurrentSettings();
        
        // Set threshold
        ThresholdSlider.Value = settings.SimilarityThreshold;
        ThresholdValueTextBox.Text = settings.SimilarityThreshold.ToString("F2");
        
        // Set API key
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            ApiKeyPasswordBox.Password = settings.ApiKey;
            ApiKeyTextBox.Text = settings.ApiKey;
        }
    }

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        
        var value = Math.Round(e.NewValue, 2);
        ThresholdValueTextBox.Text = value.ToString("F2");
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        
        if (!ShowApiKeyCheckBox.IsChecked == true)
        {
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
        }
    }

    private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        
        if (ShowApiKeyCheckBox.IsChecked == true)
        {
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
        }
    }

    private void ShowApiKeyCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (ShowApiKeyCheckBox.IsChecked == true)
        {
            ApiKeyTextBox.Visibility = Visibility.Visible;
            ApiKeyPasswordBox.Visibility = Visibility.Collapsed;
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
        }
        else
        {
            ApiKeyTextBox.Visibility = Visibility.Collapsed;
            ApiKeyPasswordBox.Visibility = Visibility.Visible;
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var threshold = ThresholdSlider.Value;
            var apiKey = ShowApiKeyCheckBox.IsChecked == true ? 
                         ApiKeyTextBox.Text : 
                         ApiKeyPasswordBox.Password;

            // Validate inputs
            if (threshold < 0.1 || threshold > 0.9)
            {
                MessageBox.Show("Threshold must be between 0.1 and 0.9", "Invalid Threshold", 
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                var result = MessageBox.Show("API Key is empty. Do you want to save anyway?", 
                                           "Empty API Key", 
                                           MessageBoxButton.YesNo, 
                                           MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Save settings
            SaveButton.IsEnabled = false;
            SaveButton.Content = "Saving...";

            await _settingsService.SaveSettingsAsync(threshold, apiKey ?? string.Empty);

            SettingsChanged = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", 
                           MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Content = "Save";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsChanged = false;
        DialogResult = false;
        Close();
    }
} 