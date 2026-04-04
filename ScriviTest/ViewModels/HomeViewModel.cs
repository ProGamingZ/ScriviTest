using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.ViewModels.Examiner; 
using System;

namespace ScriviTest.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase>? _navigateAction;

    public HomeViewModel(Action<ViewModelBase>? navigateAction = null)
    {
        _navigateAction = navigateAction;
    }

    // Tracks whether the hardware-locked RSA key has been provided
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateToExaminerCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateToExamineeCommand))]
    private bool _isActivated = false; 

    // Command to handle the Activation process
    [RelayCommand]
    private void ActivateApp()
    {
        // TODO: Implement RSA Key validation service here
        IsActivated = true;
    }

    // Navigation Commands (Only execute if IsActivated is true)
    [RelayCommand(CanExecute = nameof(IsActivated))]
    private void NavigateToExaminer()
    {
        if (_navigateAction != null)
        {
            _navigateAction(new ExaminerHubViewModel(_navigateAction));
        }
    }

    [RelayCommand(CanExecute = nameof(IsActivated))]
    private void NavigateToExaminee()
    {
        // TODO: Switch MainViewModel.CurrentPage to ExamineePreTestViewModel
    }
}