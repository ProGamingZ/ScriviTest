using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace ScriviTest.ViewModels.Examiner;

public partial class HistoryRecordWrapper : ObservableObject
{
    public HistoryRecord Record { get; }
    [ObservableProperty] private string _fileStatus = "Untested";
    [ObservableProperty] private IBrush? _statusHexColor = Brushes.Gray;
    public string DisplayKey => Record.FilePath.EndsWith(".xamk", StringComparison.OrdinalIgnoreCase) ? "NONE" : Record.ExamKey;
    public HistoryRecordWrapper(HistoryRecord record)
    {
        Record = record;
    }

}


public partial class ExamHistoryViewModel : ViewModelBase
{
    #region UI Tour
        public HelpTourViewModel HelpTour { get; } = new();

        [RelayCommand]
        private void OpenHelpTour()
        {
            HelpTour.StartTour(
                new TourStep 
                { 
                    Title = "Check Locations", 
                    TargetIcon = GetIcon("IconSettings","ℹ️"),
                    Description = "     This button when clicked verifies the location of all files listed in your history. It updates their status to 'Found ✓' or 'Missing ✕' accordingly. It automatically checks files when you enter Exam History window." 
                },
                new TourStep 
                { 
                    Title = "Relink Missing File", 
                    TargetIcon = GetIcon("IconSettings","ℹ️"), 
                    Description = "     If a file is missing (status: 'Missing ✕'), select it and click this button to open a file picker. Choose the correct .xamn or .xamk file on your storages to relink it. This updates the history record with the new path and status." 
                },
                new TourStep 
                { 
                    Title = "Delete Selected File", 
                    TargetIcon = GetIcon("IconWarning","ℹ️"), 
                    Description = "     Select a file and click this button to delete it. If the file is present on your storage, you will receive a warning prompt to confirm permanent deletion. If the file is already missing, it will simply remove the log entry from history without any prompt." 
                },
                new TourStep 
                { 
                    Title = "Table Columns Reordering", 
                    TargetIcon = GetIcon("IconSettings","ℹ️"), 
                    Description = "     You can reorder the columns in both tables by dragging and dropping the column headers. " 
                },
                new TourStep 
                { 
                    Title = "Table Columns Resizing", 
                    TargetIcon = GetIcon("IconSettings","ℹ️"), 
                    Description = "     You can resize the 'Exam Title' and 'Location' columns in both tables up to a point by dragging the edges of the column headers." 
                }
            );
        }
    #endregion

    private readonly Action<ViewModelBase> _navigateAction;

    // 1. THE TWO STACKED TABLES
    [ObservableProperty] private ObservableCollection<HistoryRecordWrapper> _questionnaireList = new();
    [ObservableProperty] private ObservableCollection<HistoryRecordWrapper> _answerKeyList = new();

    // 2. MUTUALLY EXCLUSIVE SELECTION
    [ObservableProperty] private HistoryRecordWrapper? _selectedQuestionnaire;
    [ObservableProperty] private HistoryRecordWrapper? _selectedAnswerKey;

    // When they click Table A, instantly unselect Table B!
    partial void OnSelectedQuestionnaireChanged(HistoryRecordWrapper? value)
    {
        if (value != null) SelectedAnswerKey = null; 
    }
    partial void OnSelectedAnswerKeyChanged(HistoryRecordWrapper? value)
    {
        if (value != null) SelectedQuestionnaire = null;
    }

    // The Global Helper that always returns exactly ONE selected item (or null)
    public HistoryRecordWrapper? ActiveSelection => SelectedQuestionnaire ?? SelectedAnswerKey;

    // UI Toggles
    [ObservableProperty] private bool _showDate = true;
    [ObservableProperty] private bool _showTitle = true;
    [ObservableProperty] private bool _showKey = true;
    [ObservableProperty] private bool _showStatus = true;
    [ObservableProperty] private bool _showLocation = true;

    [ObservableProperty] private bool _isDeleteWarningVisible;
    [ObservableProperty] private string _deleteWarningMessage = string.Empty;

    public ExamHistoryViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        LoadAndScanHistory();
    }


    // RESOURCE FETCH HELPERS
    private IBrush GetBrush(string resourceKey)
    {
        var app = Application.Current;
    
        if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var res) && res is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray; // Fallback
    }
    private string GetIcon(string resourceKey, string fallbackIcon)
    {
        if (Application.Current != null && Application.Current.TryGetResource(resourceKey, out var res) && res is string iconStr)
            return iconStr;
        return fallbackIcon; 
    }

    // AUTO-SCAN & LOAD ENGINE
    private void LoadAndScanHistory()
    {
        Services.AppPaths.InitializeFolders();
        QuestionnaireList.Clear();
        AnswerKeyList.Clear();

        var masterList = new List<HistoryRecord>();

        // 1. Load existing history from JSON
        if (File.Exists(Services.AppPaths.HistoryFile))
        {
            try
            {
                string jsonContent = File.ReadAllText(Services.AppPaths.HistoryFile);
                var parsedData = JsonSerializer.Deserialize<List<HistoryRecord>>(jsonContent);
                if (parsedData != null) masterList.AddRange(parsedData);
            }
            catch { /* Corrupted JSON, start fresh */ }
        }

        // 2. Auto-Scan the Data Folders
        string qFolder = Services.AppPaths.QuestionnairesDir;
        string aFolder = Services.AppPaths.AnswersDir;

        Directory.CreateDirectory(qFolder);
        Directory.CreateDirectory(aFolder);

        // Scan for .xamn and .xamk files
        var discoveredFiles = new List<string>();
        discoveredFiles.AddRange(Directory.GetFiles(qFolder, "*.xamn"));
        discoveredFiles.AddRange(Directory.GetFiles(aFolder, "*.xamk"));

        bool newFilesDiscovered = false;

        foreach (var file in discoveredFiles)
        {
            // If the hard drive file isn't in our JSON log yet, add it!
            if (!masterList.Any(r => r.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
            {
                masterList.Add(new HistoryRecord
                {
                    FilePath = file,
                    ExamTitle = Path.GetFileNameWithoutExtension(file),
                    ExportDate = File.GetCreationTime(file).ToString("yyyy-MM-dd"),
                    ExamKey = "UNKNOWN"
                });
                newFilesDiscovered = true;
            }
        }

        // 3. Split the Master List into the two UI Tables
        foreach (var record in masterList)
        {
            var wrapper = new HistoryRecordWrapper(record);
            if (record.FilePath.EndsWith(".xamn", StringComparison.OrdinalIgnoreCase))
            {
                QuestionnaireList.Add(wrapper);
            }
            else
            {
                AnswerKeyList.Add(wrapper);
            }
        }

        // 4. Save back to JSON if we discovered new files during the scan
        if (newFilesDiscovered) SaveHistoryToFile();

        // 5. Automatically verify physical locations on boot
        CheckLocations(); 
    }

    // GLOBAL BUTTON COMMANDS
    [RelayCommand]
    private void CheckLocations()
    {
        var allWrappers = QuestionnaireList.Concat(AnswerKeyList);
        foreach (var wrapper in allWrappers)
        {
            if (File.Exists(wrapper.Record.FilePath))
            {
                wrapper.FileStatus = "Found ✓";
                wrapper.StatusHexColor = GetBrush("SuccessBrush"); 
            }
            else
            {
                wrapper.FileStatus = "Missing ✕";
                wrapper.StatusHexColor = GetBrush("DangerBrush"); 
            }
        }
        ShowToast("All file locations verified.", "IconDoc", "PrimaryBrush");
    }

    public event EventHandler<HistoryRecordWrapper>? OpenFilePickerRequested;

    [RelayCommand]
    private void PromptRelink()
    {
        var target = ActiveSelection;
        if (target == null)
        {
            ShowToast("Please select a file to relink.", "IconWarning", "WarningBrush");
            return;
        }

        OpenFilePickerRequested?.Invoke(this, target);
    }

    public void UpdateFilePath(HistoryRecordWrapper wrapper, string newFilePath)
    {
        wrapper.Record.FilePath = newFilePath;
        wrapper.FileStatus = "Found ✓";
        wrapper.StatusHexColor = GetBrush("SuccessBrush");
        SaveHistoryToFile();
        ShowToast("File relinked successfully!", "IconDoc", "SuccessBrush");
    }

    [RelayCommand]
    private void AttemptDelete()
    {
        var target = ActiveSelection;
        if (target == null)
        {
            ShowToast("Please select a file to delete.", "IconWarning", "WarningBrush");
            return;
        }

        if (File.Exists(target.Record.FilePath))
        {
            // The file is physically on the drive. We must warn them!
            DeleteWarningMessage = $"Are you sure you want to PERMANENTLY delete '{Path.GetFileName(target.Record.FilePath)}' from your hard drive? This cannot be undone.";
            IsDeleteWarningVisible = true;
        }
        else
        {
            // The file is already missing. Just wipe the ghost record quietly.
            ExecuteDelete(target);
            ShowToast("Missing log removed from history.", "IconDelete", "BorderBrush");
        }
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteWarningVisible = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        var target = ActiveSelection;
        if (target != null)
        {
            try
            {
                if (File.Exists(target.Record.FilePath))
                {
                    File.Delete(target.Record.FilePath);
                }
            }
            catch (Exception ex)
            {
                ShowToast($"Error deleting file: {ex.Message}", "IconWarning", "DangerBrush");
                IsDeleteWarningVisible = false;
                return;
            }

            ExecuteDelete(target);
            ShowToast("File permanently deleted.", "IconDelete", "DangerBrush");
        }
        IsDeleteWarningVisible = false;
    }

    private void ExecuteDelete(HistoryRecordWrapper logToRemove)
    {
        if (QuestionnaireList.Contains(logToRemove)) QuestionnaireList.Remove(logToRemove);
        if (AnswerKeyList.Contains(logToRemove)) AnswerKeyList.Remove(logToRemove);
        SaveHistoryToFile();
    }

    private void SaveHistoryToFile()
    {
        try
        {
            var rawList = QuestionnaireList.Concat(AnswerKeyList).Select(w => w.Record).ToList();
            string jsonContent = JsonSerializer.Serialize(rawList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Services.AppPaths.HistoryFile, jsonContent);
        }
        catch { /* Handle save error quietly */ }
    }

    [RelayCommand]
    private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));

   
    // TOAST NOTIFICATION ENGINE
    private int _currentToastId;
    [ObservableProperty] private bool _isNotificationVisible;
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationIcon = "ℹ️";
    [ObservableProperty] private IBrush _notificationColor = Brushes.Gray;

    public async void ShowToast(string message, string iconKey, string colorKey)
    {
        NotificationMessage = message;
        NotificationIcon = GetIcon(iconKey, "ℹ️");
        NotificationColor = GetBrush(colorKey);
        IsNotificationVisible = true;

        int toastId = ++_currentToastId;
        await Task.Delay(3500);
        if (_currentToastId == toastId) IsNotificationVisible = false;
    }

    [RelayCommand]
    private void CloseToast() => IsNotificationVisible = false;
}