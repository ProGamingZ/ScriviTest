using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

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
        else if (viewModel is Examiner.ExaminerHubViewModel || viewModel is Examiner.ExamCreationViewModel)
        {
            CanResize = true;
            IsTopmost = false;
            CurrentWindowState = WindowState.Normal;
            WindowWidth = 1200; // Give the Examiner a nice wide workspace
            WindowHeight = 800;
        }
        // FUTURE Route: Examinee Test Execution (Topmost Fullscreen)
        // else if (viewModel is ExamineeTestViewModel)
        // {
        //     CanResize = false;
        //     IsTopmost = true;
        //     CurrentWindowState = WindowState.FullScreen;
        // }
    }
}