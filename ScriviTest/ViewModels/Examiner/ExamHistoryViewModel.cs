using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace ScriviTest.ViewModels.Examiner;

public partial class ExamHistoryViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;

    [ObservableProperty]
    private ObservableCollection<HistoryRecord> _historyList = new();

    public ExamHistoryViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        LoadHistory();
    }

    private void LoadHistory()
    {
        Services.AppPaths.InitializeFolders();

        if (File.Exists(Services.AppPaths.HistoryFile))
        {
            try
            {
                string jsonContent = File.ReadAllText(Services.AppPaths.HistoryFile);
                var parsedData = JsonSerializer.Deserialize<List<HistoryRecord>>(jsonContent);
                if (parsedData != null)
                {
                    HistoryList = new ObservableCollection<HistoryRecord>(parsedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not load history: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new ExaminerHubViewModel(_navigateAction));
    }
}