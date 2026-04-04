using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScriviTest.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
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
        // If successful: IsActivated = true;
    }

    // Navigation Commands (Only execute if IsActivated is true)
    [RelayCommand(CanExecute = nameof(IsActivated))]
    private void NavigateToExaminer()
    {
        // TODO: Switch MainViewModel.CurrentPage to ExaminerDashboardViewModel
    }

    [RelayCommand(CanExecute = nameof(IsActivated))]
    private void NavigateToExaminee()
    {
        // TODO: Switch MainViewModel.CurrentPage to ExamineePreTestViewModel
    }
}