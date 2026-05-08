using Avalonia.Threading;
using ScriviTest.Services;
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
    [ObservableProperty] private double _minWidth = 1280;
    [ObservableProperty] private double _minHeight = 720;
    [ObservableProperty] private double _maxWidth = double.PositiveInfinity;
    [ObservableProperty] private double _maxHeight = double.PositiveInfinity;

    public MainWindowViewModel()
    {
        var initialPage = new HomeViewModel(Navigate);
        _currentPage = initialPage; 
        Navigate(initialPage);
        StartGlobalSecuritySweep();
    }

    private void StartGlobalSecuritySweep()
    {
        var securityTimer = new DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        securityTimer.Tick += (s, e) =>
        {
            // If they are on the Home screen or Examinee screens, we don't need to kick them.
            // Examinees don't need an active license to take tests.
            if (CurrentPage is HomeViewModel || CurrentPage is ExamineeHubViewModel || CurrentPage is ExamineeTestViewModel)
            {
                return;
            }

            // If they are anywhere else (Examiner Hub, History, Grading) and the license is invalid:
            if (!LicenseManager.IsLicenseValid())
            {
                // Forcefully navigate them back to the Home Screen (which acts as the lock screen)
                Navigate(new HomeViewModel(Navigate));
            }
        };
        securityTimer.Start();
    }

    private void Navigate(ViewModelBase viewModel)
    {
        // Route: Home Screen (Strict, Fixed Window)
        if (viewModel is HomeViewModel)
        {
            CanResize = false;
            IsTopmost = false;
            CurrentWindowState = WindowState.Normal;
            MaxWidth = 800;  
            MaxHeight = 450;
            MinWidth = 800;  
            MinHeight = 450;
            WindowWidth = 800;
            WindowHeight = 450;
        }
        // Route: Examinee Test Execution (Strict Fullscreen Lockdown)
        else if (viewModel is ExamineeTestViewModel)
        {
            CanResize = false;
            IsTopmost = true; 
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            CurrentWindowState = WindowState.FullScreen; 
        }
        // Route: ALL OTHER HUBS (Responsive, Resizable, 1024x768 Minimum)
        else 
        {
            CanResize = true;
            IsTopmost = false;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            MinWidth = 1024;
            MinHeight = 768;
            CurrentWindowState = WindowState.Maximized; 
        }
        CurrentPage = viewModel;
    }
}