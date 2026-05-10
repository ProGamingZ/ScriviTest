using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using Avalonia.Media;

namespace ScriviTest.Models;

public partial class ReviewQuestion : ObservableObject
{
    public int QuestionNumber { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; 

    public bool IsEssay => Type == "Essay";
    public bool IsObjective => Type != "Essay";
    public int MaxPoints { get; set; }
    
    public string EssayResponse { get; set; } = string.Empty;
    public Avalonia.Media.Imaging.Bitmap? ImageBitmap { get; set; }
    public List<ReviewChoice> Choices { get; set; } = new();
    [ObservableProperty] private string _remarks = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridIcon))]
    [NotifyPropertyChangedFor(nameof(GridIconColor))]
    [NotifyPropertyChangedFor(nameof(PointsInputText))]
    private double _pointsAwarded;
    private string? _uiTextOverride;
    public string PointsInputText 
    {
        get => _uiTextOverride ?? PointsAwarded.ToString();
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _uiTextOverride = "";
                PointsAwarded = 0;
            }
            else if (double.TryParse(value, out double parsed))
            {
                double clamped = Math.Clamp(parsed, 0, MaxPoints);
                double rounded = Math.Round(clamped, 1);
                PointsAwarded = rounded;
                
                if (parsed != clamped || parsed != rounded)
                {
                    _uiTextOverride = rounded.ToString();
                }
                else
                {
                    _uiTextOverride = value;
                }
            }
            
            OnPropertyChanged(nameof(PointsInputText));
        }
    }
    public string GridIcon => (IsEssay && PointsAwarded == 0) ? "!" : (PointsAwarded > 0 ? "✓" : "X");
    public string GridIconColor => (IsEssay && PointsAwarded == 0) ? "#2196F3" : (PointsAwarded > 0 ? "#4CAF50" : "#F44336");
}

public class ReviewSection
{
    public string Title { get; set; } = string.Empty;
    // Holds questions for specific sections
    public System.Collections.ObjectModel.ObservableCollection<ReviewQuestion> Questions { get; set; } = new();
}

public class ReviewChoice : ObservableObject
{
    public string Text { get; set; } = string.Empty;
    public bool IsStudentSelected { get; set; }
    public bool IsCorrectAnswer { get; set; }
    public Avalonia.Media.Imaging.Bitmap? ImageBitmap { get; set; }
    public bool IsSingleSelection { get; set; }
    
    public Avalonia.CornerRadius BoxCornerRadius => IsSingleSelection ? new Avalonia.CornerRadius(15) : new Avalonia.CornerRadius(4);

    public bool HasAnswerKey { get; set; } = true;
    public string InnerSymbol => (HasAnswerKey, IsStudentSelected, IsCorrectAnswer) switch
    {
        (false, true, _) => "●",
        (true, true, true) => "✓",
        (true, true, false) => "✕",
        (true, false, true) => "?",
        _ => ""
    };

    public IBrush BoxBackgroundColor => (IsStudentSelected, IsCorrectAnswer) switch
    {
        (true, true)  => new SolidColorBrush(Color.Parse("#4CAF50")),
        (true, false) => new SolidColorBrush(Color.Parse("#F44336")),
        (false, true) => new SolidColorBrush(Color.Parse("#2196F3")),
        _ => Brushes.Transparent
    };

    // If it's completely unselected/neutral, give it a gray border.
    public IBrush BoxBorderColor => (IsStudentSelected, IsCorrectAnswer) == (false, false) 
    ? new SolidColorBrush(Color.Parse("#9E9E9E")) 
    : Brushes.Transparent;
}