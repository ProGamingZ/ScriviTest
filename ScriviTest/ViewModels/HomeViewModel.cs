using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.ViewModels.Examiner; 
using ScriviTest.ViewModels.Examinee;
using ScriviTest.Views; 
using ScriviTest.Services; 
using System;
using System.Threading.Tasks;

namespace ScriviTest.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase>? _navigateAction;

    public HomeViewModel(Action<ViewModelBase>? navigateAction = null)
    {
        _navigateAction = navigateAction;
        IsActivated = LicenseManager.IsLicenseValid();
    }

    // We tell MVVM to update the IsNotActivated boolean whenever IsActivated changes!
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateToExaminerCommand))]
    [NotifyPropertyChangedFor(nameof(IsNotActivated))] 
    private bool _isActivated = false; 

    // This computed property acts as the reverse switch for your UI to hide the Activate button
    public bool IsNotActivated => !IsActivated;

    [RelayCommand]
    private async Task ActivateApp()
    {
        if (IsActivated) return;

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow == null) return;

            var dialog = new ActivationWindow();
            var success = await dialog.ShowDialog<bool>(mainWindow);

            if (success)
            {
                IsActivated = true; 
            }
        }
    }

    [RelayCommand(CanExecute = nameof(IsActivated))]
    private void NavigateToExaminer()
    {
        if (_navigateAction != null)
        {
            _navigateAction(new ExaminerHubViewModel(_navigateAction));
        }
    }

    // REMOVED the CanExecute restriction here. Examinees can ALWAYS click this!
    [RelayCommand]
    private void NavigateToExaminee()
    {
        if (_navigateAction != null)
        {
            _navigateAction(new ExamineeHubViewModel(_navigateAction));
        }
    }
}