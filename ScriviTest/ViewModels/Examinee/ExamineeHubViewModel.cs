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
    private readonly Services.CryptographyService _cryptoService;

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
        _cryptoService = new Services.CryptographyService();
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
        ErrorMessage = "Decrypting...";

        // 1. Create a secure temporary folder in the OS's temp directory
        string sessionGuid = Guid.NewGuid().ToString();
        string tempImageFolder = Path.Combine(Path.GetTempPath(), "ScriviTest_Session", sessionGuid);

        // 2. Attempt Decryption
        var decryptedExam = _cryptoService.DecryptAndExtractExam(SelectedFilePath!, WhiteboardKey.ToUpper(), tempImageFolder);

        if (decryptedExam == null)
        {
            // If it fails, the password was wrong or the file was tampered with.
            ErrorMessage = "Access Denied. Invalid Whiteboard Key or corrupted file.";
            return;
        }

        // 3. Success! Pass the decrypted data and the image folder to the actual Test UI
        ErrorMessage = string.Empty;
        _navigateAction(new ExamineeTestViewModel(_navigateAction, decryptedExam, tempImageFolder, WhiteboardKey.ToUpper()));    }
}