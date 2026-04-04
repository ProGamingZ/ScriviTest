using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models; // Added to access the Question model
using System;
using System.Collections.ObjectModel;
using System.IO; // Path operations
using System.Threading.Tasks; // async Tasks


namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService; // The new service

    // This is the list the UI will loop through
    public ObservableCollection<Question> Questions { get; } = new();

    public ExamCreationViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService(); // Initialize it

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

    [RelayCommand]
    private async Task AttachImage(Question targetQuestion)
    {
        if (targetQuestion == null) return;

        // 1. Open the file picker
        var selectedFilePath = await _fileService.PickImageAsync();

        // 2. If the user picked a file, apply it to the Question model
        if (!string.IsNullOrEmpty(selectedFilePath))
        {
            targetQuestion.AttachedImageFullPath = selectedFilePath;
            targetQuestion.AttachedImageFileName = Path.GetFileName(selectedFilePath); // Extracts just "diagram.png"
        }
    }
    
    // Command to clear the image if the user clicks the 'X'
    [RelayCommand]
    private void RemoveImage(Question targetQuestion)
    {
        if (targetQuestion == null) return;
        targetQuestion.AttachedImageFullPath = null;
        targetQuestion.AttachedImageFileName = null;
    }
}