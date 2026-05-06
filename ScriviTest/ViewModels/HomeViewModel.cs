using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.ViewModels.Examiner; 
using ScriviTest.ViewModels.Examinee;
using ScriviTest.Views; 
using ScriviTest.Services; 
using System.Text.Json;
using System;
using System.IO;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Threading.Tasks;


namespace ScriviTest.ViewModels;

// A tiny class to hold our saved settings
public class AppSettings
{
    public bool IsDarkMode { get; set; }
}

public partial class HomeViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isDarkMode;

    #region --- THEME PERSISTENCE LOGIC ---
    
    // --- Helper to get the save file path ---
    private string GetSettingsFilePath()
    {
        // Saves to: C:\Users\YourName\AppData\Roaming\ScriviTest\settings.json
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "ScriviTest");
        Directory.CreateDirectory(appFolder); // Ensures the folder exists
        return Path.Combine(appFolder, "settings.json");
    }

    // --- LOAD LOGIC ---
    private void LoadThemeState()
    {
        string path = GetSettingsFilePath();
        
        if (File.Exists(path))
        {
            // If the user saved a preference previously, load it!
            try 
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                _isDarkMode = settings?.IsDarkMode ?? false;
            } 
            catch 
            { 
                _isDarkMode = false; // Fallback if file is corrupted
            }
        }
        else
        {
            // BUG FIX: If no save file exists, check the actual OS system theme so the toggle matches!
            var app = Application.Current;
            if (app != null)
            {
                _isDarkMode = app.ActualThemeVariant == ThemeVariant.Dark;
            }
        }

        // Apply the loaded/detected theme immediately
        ApplyTheme(_isDarkMode);
    }

    // --- SAVE LOGIC ---
    private void SaveThemeState(bool isDark)
    {
        try 
        {
            var settings = new AppSettings { IsDarkMode = isDark };
            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText(GetSettingsFilePath(), json);
        } 
        catch 
        { 
            // Silently ignore save errors (e.g., file locked by OS)
        }
    }

    // --- TOGGLE EVENT LOGIC ---
    // This runs automatically whenever the user clicks the ToggleSwitch
    partial void OnIsDarkModeChanged(bool value)
    {
        ApplyTheme(value);
        SaveThemeState(value); // Save it to the hard drive!
    }

    // --- THEME APPLIER ---
    private void ApplyTheme(bool isDark)
    {
        var app = Application.Current;
        if (app != null)
        {
            app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
    
    #endregion

    private readonly Action<ViewModelBase>? _navigateAction;
    private readonly string _warningCacheFile = Path.Combine(Services.AppPaths.RootAppFolder, "warning_cache.dat");

    public HomeViewModel(Action<ViewModelBase>? navigateAction = null)
    {
        // 1. RUN THE THEME LOADER FIRST!
        // This ensures the UI renders with the correct colors instantly on boot.
        LoadThemeState();

        _navigateAction = navigateAction;
        Services.AppPaths.InitializeFolders();
        IsActivated = LicenseManager.IsLicenseValid();
        
        if (IsActivated)
        {
            CheckSubscriptionAlerts();
        }
    }

    // We tell MVVM to update the IsNotActivated boolean whenever IsActivated changes!
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NavigateToExaminerCommand))]
    [NotifyPropertyChangedFor(nameof(IsNotActivated))] 
    private bool _isActivated = false; 

    // This computed property acts as the reverse switch for your UI to hide the Activate button
    public bool IsNotActivated => !IsActivated;

    // --- WARNING UI PROPERTIES ---
    [ObservableProperty] private bool _isExpirationWarningVisible = false;
    [ObservableProperty] private string _expirationWarningMessage = string.Empty;

    [RelayCommand]
    private void CloseWarning()
    {
        IsExpirationWarningVisible = false;
        _hasUserDismissedWarning = true;
    }

    private DispatcherTimer? _countdownTimer;
    private bool _hasUserDismissedWarning = false;
    private void CheckSubscriptionAlerts()
    {
        DateTime expirationDate = LicenseManager.GetExpirationDate();
        if (expirationDate == DateTime.MinValue) return;

        TimeSpan remaining = expirationDate - DateTime.Now;
        int daysLeft = (int)remaining.TotalDays;
        
        // Condition 1: License is already dead
        if (remaining.TotalSeconds <= 0)
        {
            IsActivated = false;
            return;
        }

        // Condition 2: Less than 24 hours (Start the LIVE Countdown)
        if (remaining.TotalHours <= 24)
        {
            StartCountdownTimer();
            return; // Exit early so we don't trigger the daily/weekly alerts
        }

        // Condition 3: Standard Daily/Weekly warnings
        if (daysLeft > 30) return;

        DateTime lastWarningDate = GetLastWarningDate();

        if (daysLeft <= 7 && lastWarningDate.Date < DateTime.Today)
        {
            ShowWarning($"URGENT: Your ScriviTest subscription expires in {daysLeft} days! Please renew soon to avoid losing access.");
        }
        else if (daysLeft <= 30 && (DateTime.Now - lastWarningDate).TotalDays >= 7)
        {
            ShowWarning($"Reminder: Your ScriviTest subscription will expire in {daysLeft} days.");
        }
    }
    private void StartCountdownTimer()
    {
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (s, e) =>
        {
            TimeSpan timeLeft = LicenseManager.GetExpirationDate() - DateTime.Now;

            if (timeLeft.TotalSeconds <= 0)
            {
                // The clock hit zero! Stop the timer, hide the warning, and lock the app.
                _countdownTimer.Stop();
                IsExpirationWarningVisible = false;
                IsActivated = false;
            }
            else
            {
                // Format the time as HH:MM:SS and update the string
                string timeString = timeLeft.ToString(@"hh\:mm\:ss");
                ExpirationWarningMessage = $"URGENT: Your ScriviTest subscription expires in {timeString}!\n\nPlease renew immediately.";
                
                // ONLY open the UI if the user hasn't clicked Acknowledge yet
                if (!_hasUserDismissedWarning)
                {
                    IsExpirationWarningVisible = true;
                }
            }
        };
        _countdownTimer.Start();
    }

    private void ShowWarning(string message)
    {
        ExpirationWarningMessage = message;
        IsExpirationWarningVisible = true;
        try { File.WriteAllText(_warningCacheFile, DateTime.Now.ToString()); } catch { }
    }

    private DateTime GetLastWarningDate()
    {
        try
        {
            if (File.Exists(_warningCacheFile))
                return DateTime.Parse(File.ReadAllText(_warningCacheFile));
        }
        catch { }
        return DateTime.MinValue; 
    }

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