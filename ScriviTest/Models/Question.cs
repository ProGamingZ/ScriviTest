using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ScriviTest.Models;

public partial class Question : ObservableObject
{
    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMultipleChoice))]
    [NotifyPropertyChangedFor(nameof(IsTrueFalse))]
    [NotifyPropertyChangedFor(nameof(IsMultipleAnswer))]
    [NotifyPropertyChangedFor(nameof(IsEssay))]
    private QuestionType _type = QuestionType.MultipleChoice;

    // These booleans dynamically evaluate based on the current Type
    public bool IsMultipleChoice => Type == QuestionType.MultipleChoice;
    public bool IsTrueFalse => Type == QuestionType.TrueFalse;
    public bool IsMultipleAnswer => Type == QuestionType.MultipleAnswer;
    public bool IsEssay => Type == QuestionType.Essay;

    [ObservableProperty]
    private int _points = 1;

    // Media Integration (The Zip Method)
    [ObservableProperty]
    private string? _attachedImageFileName; 

    // Settings for Multiple Choice / True-False / Multiple Answer
    [ObservableProperty]
    private bool _shuffleChoices = false;

    public ObservableCollection<Choice> Choices { get; } = new();

    // Specific to Multiple Answer
    [ObservableProperty]
    private ScoringRubric _multipleAnswerRubric = ScoringRubric.AllOrNothing;

    // Specific to Essay
    [ObservableProperty]
    private int _maxWordCount = 500;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandCollapseIcon))]
    private bool _isExpanded = true; // Defaults to true so new questions open instantly

    public string ExpandCollapseIcon => IsExpanded ? "-" : "+";

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }
}