using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

using ScriviTest.ViewModels.Examinee;
using ScriviTest.ViewModels.Examiner;

namespace ScriviTest.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    // Dynamic Window Properties
    [ObservableProperty] private bool _canResize = false;
    [ObservableProperty] private WindowState _currentWindowState = WindowState.Normal;
    [ObservableProperty] private bool _isTopmost = false;
    [ObservableProperty] private double _windowWidth = 800;
    [ObservableProperty] private double _windowHeight = 450;

    public MainWindowViewModel()
    {
        // Start on the Home Screen with strict dimensions
        _currentPage = new HomeViewModel(Navigate);
    }

    private void Navigate(ViewModelBase viewModel)
    {
        CurrentPage = viewModel;

        // Route: Home Screen (Strict, Fixed Window)
        if (viewModel is HomeViewModel)
        {
            CanResize = false;
            IsTopmost = false;
            CurrentWindowState = WindowState.Normal;
            WindowWidth = 800;
            WindowHeight = 450;
        }
        // Route: Examiner Hub & Creation (Resizable, Standard Window)
        else if (viewModel is ExaminerHubViewModel || viewModel is ExamCreationViewModel)
        {
            CanResize = true;
            IsTopmost = false;
            CurrentWindowState = WindowState.Maximized;
        }
        // NEW Route: Examinee Hub (Maximized but Resizable)
        else if (viewModel is ExamineeHubViewModel)
        {
            CanResize = true;
            IsTopmost = false;
            CurrentWindowState = WindowState.Maximized; // Fills the screen, but keeps the taskbar and window controls
        }
        // NEW Route: Examinee Test Execution (Strict Fullscreen Lockdown)
        else if (viewModel is ExamineeTestViewModel)
        {
            CanResize = false;
            IsTopmost = true; // Prevents other apps from opening on top of it
            CurrentWindowState = WindowState.FullScreen; // Completely hides the taskbar and locks the screen bounds
        }
    }
}