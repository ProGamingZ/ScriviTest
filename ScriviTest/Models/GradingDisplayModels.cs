using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

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


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridIcon))]
    [NotifyPropertyChangedFor(nameof(GridIconColor))]
    [NotifyPropertyChangedFor(nameof(PointsInputText))]
    private double _pointsAwarded;
    public string PointsInputText
    {
        get => PointsAwarded.ToString();
        set
        {
            // If the examiner backspaces everything, safely default to 0 to prevent a crash
            if (string.IsNullOrWhiteSpace(value))
            {
                PointsAwarded = 0;
            }
            // Try to parse what they typed into a valid number
            else if (double.TryParse(value, out double parsed))
            {
                PointsAwarded = parsed;
            }
            // (If they accidentally type letters like "5a", it safely ignores the letter!)
            
            OnPropertyChanged(nameof(PointsInputText));
        }
    }
    public string GridIcon => (IsEssay && PointsAwarded == 0) ? "!" : (PointsAwarded > 0 ? "✓" : "X");
    public string GridIconColor => (IsEssay && PointsAwarded == 0) ? "#2196F3" : (PointsAwarded > 0 ? "#4CAF50" : "#F44336");
}

public class ReviewSection
{
    // Holds questions for specific sections
    public System.Collections.ObjectModel.ObservableCollection<ReviewQuestion> Questions { get; set; } = new();
}

public class ReviewChoice
{
    public string Text { get; set; } = string.Empty;
    public bool IsStudentSelected { get; set; }
    public bool IsCorrectAnswer { get; set; }
    public Avalonia.Media.Imaging.Bitmap? ImageBitmap { get; set; }
    public bool IsSingleSelection { get; set; }
    
    public Avalonia.CornerRadius BoxCornerRadius => IsSingleSelection ? new Avalonia.CornerRadius(15) : new Avalonia.CornerRadius(4);

    // Inner symbol (Clean text characters, not emojis!)
    public string InnerSymbol => (IsStudentSelected, IsCorrectAnswer) switch
    {
        (true, true) => "✓",
        (true, false) => "✕",
        (false, true) => "?",
        _ => ""
    };

    public string BoxBackgroundColor => (IsStudentSelected, IsCorrectAnswer) switch
    {
        (true, true) => "#4CAF50", // Green
        (true, false) => "#F44336", // Red
        (false, true) => "#2196F3", // Blue
        _ => "Transparent"
    };

    // If it's completely unselected/neutral, give it a gray border.
    public string BoxBorderColor => (IsStudentSelected, IsCorrectAnswer) == (false, false) ? "Gray" : "Transparent";
}