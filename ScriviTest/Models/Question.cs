using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ScriviTest.Models;

public partial class Question : ObservableObject
{
    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private QuestionType _type = QuestionType.MultipleChoice;

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