using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models; // Added to access the Question model
using System;
using System.Collections.ObjectModel;

namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;

    // This is the list the UI will loop through
    public ObservableCollection<Question> Questions { get; } = new();

    public ExamCreationViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;

        // Add one empty question by default so the screen isn't totally blank
        Questions.Add(new Question());
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new ExaminerHubViewModel(_navigateAction));
    }

    // Commands for the Dropdown Menu
    [RelayCommand]
    private void AddMultipleChoice()
    {
        Questions.Add(new Question { Type = QuestionType.MultipleChoice });
    }

    [RelayCommand]
    private void AddTrueFalse()
    {
        Questions.Add(new Question { Type = QuestionType.TrueFalse });
    }

    [RelayCommand]
    private void AddMultipleAnswer()
    {
        Questions.Add(new Question { Type = QuestionType.MultipleAnswer });
    }

    [RelayCommand]
    private void AddEssay()
    {
        Questions.Add(new Question { Type = QuestionType.Essay });
    }

    [RelayCommand]
    private void RemoveQuestion(Question questionToRemove)
    {
        if (Questions.Contains(questionToRemove))
        {
            Questions.Remove(questionToRemove);
        }
    }
}