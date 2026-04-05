using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ScriviTest.Models;

public partial class Question : ObservableObject
{
    public string GroupId { get; } = System.Guid.NewGuid().ToString();
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

    // --- True/False Answer Tracking ---
    private bool _isTrueFalseAnswerTrue = true;

    public bool IsTrueFalseAnswerTrue
    {
        get => _isTrueFalseAnswerTrue;
        set
        {
            if (SetProperty(ref _isTrueFalseAnswerTrue, value))
            {
                OnPropertyChanged(nameof(IsTrueFalseAnswerFalse));
            }
        }
    }

    public bool IsTrueFalseAnswerFalse
    {
        get => !_isTrueFalseAnswerTrue;
        set
        {
            if (value) IsTrueFalseAnswerTrue = false;
        }
    }

    [ObservableProperty]
    private int _points = 1;

    public string? AttachedImageFullPath { get; set; }
    // Media Integration (The Zip Method)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _attachedImageFileName;

    public bool HasImage => !string.IsNullOrEmpty(AttachedImageFileName); 

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

    public Question()
    {
        Choices.Add(new Choice { Text = "Option 1" });
        Choices.Add(new Choice { Text = "Option 2" });
    }

    [RelayCommand]
    private void AddChoice()
    {
        Choices.Add(new Choice { Text = $"Option {Choices.Count + 1}" });
    }

    [RelayCommand]
    private void RemoveChoice(Choice choiceToRemove)
    {
        // Prevent the examiner from deleting all choices (keep at least 2)
        if (Choices.Count > 2)
        {
            Choices.Remove(choiceToRemove);
        }
    }
}