using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ScriviTest.ViewModels.Examiner;

// 1. THE UI WRAPPER
// This holds the original record, plus temporary UI states for the DataGrid.
public partial class HistoryRecordWrapper : ObservableObject
{
    public HistoryRecord Record { get; }

    [ObservableProperty] private bool _isSelected;
    
    [ObservableProperty] private string _fileStatus = "Untested";
    [ObservableProperty] private string _statusHexColor = "Gray";

    public HistoryRecordWrapper(HistoryRecord record)
    {
        Record = record;
    }
}

public partial class ExamHistoryViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;

    // 2. CHANGE TO OBSERVABLE COLLECTION OF WRAPPERS
    [ObservableProperty]
    private ObservableCollection<HistoryRecordWrapper> _historyList = new();

    // --- COLUMN VISIBILITY SETTINGS ---
    [ObservableProperty] private bool _showDate = true;
    [ObservableProperty] private bool _showTitle = true;
    [ObservableProperty] private bool _showKey = true;
    [ObservableProperty] private bool _showStatus = true;
    [ObservableProperty] private bool _showLocation = true;



    public ExamHistoryViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        LoadHistory();
    }

    private void LoadHistory()
    {
        Services.AppPaths.InitializeFolders();
        HistoryList.Clear();

        if (File.Exists(Services.AppPaths.HistoryFile))
        {
            try
            {
                string jsonContent = File.ReadAllText(Services.AppPaths.HistoryFile);
                var parsedData = JsonSerializer.Deserialize<List<HistoryRecord>>(jsonContent);
                if (parsedData != null)
                {
                    // Wrap each raw record before adding it to the UI list
                    foreach (var record in parsedData)
                    {
                        HistoryList.Add(new HistoryRecordWrapper(record));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not load history: {ex.Message}");
            }
        }
    }

    [ObservableProperty] private HistoryRecordWrapper? _selectedWrapper;
    [ObservableProperty] private bool _isDeleteWarningVisible;
    [ObservableProperty] private string _deleteWarningMessage = string.Empty;

    // --- 1. CHECK LOCATIONS ---
    [RelayCommand]
    private void CheckLocations()
    {
        foreach (var wrapper in HistoryList)
        {
            if (File.Exists(wrapper.Record.FilePath))
            {
                wrapper.FileStatus = "Found ✓";
                wrapper.StatusHexColor = "#4CAF50"; // Green
            }
            else
            {
                wrapper.FileStatus = "Missing ✕";
                wrapper.StatusHexColor = "#F44336"; // Red
            }
        }
    }

    // --- 2. DELETE LOGIC ---
    [RelayCommand]
    private void AttemptDelete()
    {
        // Find all wrappers where the Checkbox is ticked
        var selectedLogs = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(HistoryList, x => x.IsSelected));
        if (selectedLogs.Count == 0) return;

        // Check the hard drive to see if any of those physical files still exist
        int existingFilesCount = System.Linq.Enumerable.Count(selectedLogs, x => File.Exists(x.Record.FilePath));

        if (existingFilesCount > 0)
        {
            // Trigger the hidden UI warning overlay
            DeleteWarningMessage = $"{existingFilesCount} of the {selectedLogs.Count} selected logs point to files that still exist on your computer. Deleting the log will NOT delete the actual file.";
            IsDeleteWarningVisible = true;
        }
        else
        {
            // If the files are already gone, it's safe to delete the logs immediately
            ExecuteDelete(selectedLogs);
        }
    }

    [RelayCommand]
    private void CancelDelete() => IsDeleteWarningVisible = false;

    [RelayCommand]
    private void ConfirmDelete()
    {
        var selectedLogs = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(HistoryList, x => x.IsSelected));
        ExecuteDelete(selectedLogs);
        IsDeleteWarningVisible = false;
    }

    private void ExecuteDelete(List<HistoryRecordWrapper> logsToRemove)
    {
        foreach (var log in logsToRemove) HistoryList.Remove(log);
        SaveHistoryToFile();
    }

    // --- 3. RELINK LOGIC ---
    
    // We fire an event so the View knows to open the Windows/Mac File Picker
    public event EventHandler<HistoryRecordWrapper>? OpenFilePickerRequested;

    [RelayCommand]
    private void PromptRelink()
    {
        if (SelectedWrapper != null)
        {
            OpenFilePickerRequested?.Invoke(this, SelectedWrapper);
        }
    }

    // The View calls this after the teacher picks the new file
    public void UpdateFilePath(HistoryRecordWrapper wrapper, string newFilePath)
    {
        wrapper.Record.FilePath = newFilePath;
        wrapper.FileStatus = "Found ✓";
        wrapper.StatusHexColor = "#4CAF50";
        SaveHistoryToFile();
    }

    // --- 4. SAVE TO FILE ---
    private void SaveHistoryToFile()
    {
        try
        {
            // Extract the pure DTOs back out of the Wrappers
            var rawList = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(HistoryList, w => w.Record));
            string jsonContent = JsonSerializer.Serialize(rawList, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Services.AppPaths.HistoryFile, jsonContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not save history: {ex.Message}");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new ExaminerHubViewModel(_navigateAction));
    }
}