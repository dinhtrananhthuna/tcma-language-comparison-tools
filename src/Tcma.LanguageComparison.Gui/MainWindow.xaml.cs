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
using Tcma.LanguageComparison.Gui.ViewModels;

namespace Tcma.LanguageComparison.Gui;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Lấy ViewModel từ DI container
        var viewModel = App.ServiceProvider?.GetService(typeof(ViewModels.MainWindowViewModel)) as ViewModels.MainWindowViewModel;
        DataContext = viewModel;
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