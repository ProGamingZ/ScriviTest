using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ScriviTest.ViewModels.Examiner;

public partial class GradingHubViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;

    [ObservableProperty]
    private string? _answerKeyPath;

    [ObservableProperty]
    private string _answerKeyFileName = "No answer key selected.";

    [ObservableProperty]
    private List<string> _studentSubmissionPaths = new();

    [ObservableProperty]
    private string _studentFilesSummary = "0 student submissions loaded.";

    [ObservableProperty]
    private string _whiteboardKey = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public GradingHubViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService();
    }

    // Assuming the user navigates here from the Examiner Hub
    [RelayCommand]
    private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));

    [RelayCommand]
    private async Task BrowseForAnswerKey()
    {
        ErrorMessage = string.Empty;
        var path = await _fileService.PickAnswerKeyAsync();
        if (!string.IsNullOrEmpty(path))
        {
            AnswerKeyPath = path;
            AnswerKeyFileName = Path.GetFileName(path);
        }
    }

    [RelayCommand]
    private async Task BrowseForStudentFiles()
    {
        ErrorMessage = string.Empty;
        var paths = await _fileService.PickStudentSubmissionsAsync();
        if (paths.Count > 0)
        {
            StudentSubmissionPaths = paths;
            StudentFilesSummary = $"{paths.Count} student submission(s) loaded for grading.";
        }
    }

    private bool CanStartGrading => !string.IsNullOrEmpty(AnswerKeyPath) && StudentSubmissionPaths.Count > 0 && WhiteboardKey.Length >= 6;

    partial void OnAnswerKeyPathChanged(string? value) => StartGradingCommand.NotifyCanExecuteChanged();
    partial void OnStudentSubmissionPathsChanged(List<string> value) => StartGradingCommand.NotifyCanExecuteChanged();
    partial void OnWhiteboardKeyChanged(string value) => StartGradingCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanStartGrading))]
    private void StartGrading()
    {
        // STEP 2 WILL GO HERE: Batch Decryption and Grading
        Console.WriteLine($"Starting Auto-Grader with {StudentSubmissionPaths.Count} files...");
    }
}