using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScriviTest.ViewModels.Examiner;

public partial class HistoryRecordWrapper : ObservableObject
{
    public HistoryRecord Record { get; }
    [ObservableProperty] private string _fileStatus = "Untested";
    [ObservableProperty] private string _statusHexColor = "Gray";
    public string DisplayKey => Record.FilePath.EndsWith(".xamk", StringComparison.OrdinalIgnoreCase) ? "NONE" : Record.ExamKey;
    public HistoryRecordWrapper(HistoryRecord record)
    {
        Record = record;
    }
}

public partial class ExamHistoryViewModel : ViewModelBase
{
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

    // ==========================================
    // AUTO-SCAN & LOAD ENGINE
    // ==========================================
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
        string baseDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        string qFolder = Path.Combine(baseDataPath, "Questionnaires");
        string aFolder = Path.Combine(baseDataPath, "Answers");

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

    // ==========================================
    // GLOBAL BUTTON COMMANDS
    // ==========================================

    [RelayCommand]
    private void CheckLocations()
    {
        var allWrappers = QuestionnaireList.Concat(AnswerKeyList);
        foreach (var wrapper in allWrappers)
        {
            if (File.Exists(wrapper.Record.FilePath))
            {
                wrapper.FileStatus = "Found ✓";
                wrapper.StatusHexColor = "#4CAF50"; 
            }
            else
            {
                wrapper.FileStatus = "Missing ✕";
                wrapper.StatusHexColor = "#F44336"; 
            }
        }
        ShowToast("All file locations verified.", "🔍", "#1976D2");
    }

    public event EventHandler<HistoryRecordWrapper>? OpenFilePickerRequested;

    [RelayCommand]
    private void PromptRelink()
    {
        var target = ActiveSelection;
        if (target == null)
        {
            ShowToast("Please select a file to relink.", "⚠️", "#F57C00");
            return;
        }

        OpenFilePickerRequested?.Invoke(this, target);
    }

    public void UpdateFilePath(HistoryRecordWrapper wrapper, string newFilePath)
    {
        wrapper.Record.FilePath = newFilePath;
        wrapper.FileStatus = "Found ✓";
        wrapper.StatusHexColor = "#4CAF50";
        SaveHistoryToFile();
        ShowToast("File relinked successfully!", "🔗", "#388E3C");
    }

    [RelayCommand]
    private void AttemptDelete()
    {
        var target = ActiveSelection;
        if (target == null)
        {
            ShowToast("Please select a file to delete.", "⚠️", "#F57C00");
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
            ShowToast("Missing log removed from history.", "🧹", "#607D8B");
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
                ShowToast($"Error deleting file: {ex.Message}", "🛑", "#D32F2F");
                IsDeleteWarningVisible = false;
                return;
            }

            ExecuteDelete(target);
            ShowToast("File permanently deleted.", "🗑️", "#D32F2F");
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

    // ==========================================
    // TOAST NOTIFICATION ENGINE
    // ==========================================
    private int _currentToastId;
    [ObservableProperty] private bool _isNotificationVisible;
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationIcon = "ℹ️";
    [ObservableProperty] private string _notificationColor = "#323232";

    public async void ShowToast(string message, string icon = "ℹ️", string hexColor = "#323232")
    {
        NotificationMessage = message;
        NotificationIcon = icon;
        NotificationColor = hexColor;
        IsNotificationVisible = true;

        int toastId = ++_currentToastId;
        await Task.Delay(3500);
        if (_currentToastId == toastId) IsNotificationVisible = false;
    }

    [RelayCommand]
    private void CloseToast() => IsNotificationVisible = false;
}