using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ScriviTest.ViewModels.Examinee;

public partial class ExamineeHubViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;

    [ObservableProperty]
    private string? _selectedFilePath;

    [ObservableProperty]
    private string _selectedFileName = "No file selected.";

    [ObservableProperty]
    private string _whiteboardKey = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public ExamineeHubViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService();
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new HomeViewModel(_navigateAction));
    }

    [RelayCommand]
    private async Task BrowseForExam()
    {
        ErrorMessage = string.Empty; // Clear old errors
        
        var selectedPath = await _fileService.PickExamArchiveAsync(); 
        
        if (!string.IsNullOrEmpty(selectedPath))
        {
            SelectedFilePath = selectedPath;
            SelectedFileName = Path.GetFileName(selectedPath);
        }
    }

    // This button only becomes clickable when the file is loaded and the key is typed!
    private bool CanStartExam => !string.IsNullOrEmpty(SelectedFilePath) && WhiteboardKey.Length >= 6;

    // We tell the UI to re-evaluate the Start button whenever the Key or File changes
    partial void OnWhiteboardKeyChanged(string value) => StartExamCommand.NotifyCanExecuteChanged();
    partial void OnSelectedFilePathChanged(string? value) => StartExamCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanStartExam))]
    private void StartExam()
    {
        // STEP 2 WILL GO HERE: We will pass the FilePath and the Key into the Decryption engine!
        Console.WriteLine($"Starting Exam with File: {SelectedFilePath} and Key: {WhiteboardKey}");
    }
}