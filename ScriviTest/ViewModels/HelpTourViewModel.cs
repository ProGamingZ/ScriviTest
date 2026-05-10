using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ScriviTest.ViewModels;

// 1. The class that represents a single "Page" in the tour
public class TourStep
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // An icon (like a save icon, a plus icon, etc.) so the user knows what UI element we are talking about
    public string TargetIcon { get; set; } = string.Empty; 
}

// 2. The reusable ViewModel that controls the overlay
public partial class HelpTourViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isOpen = false;
    [ObservableProperty] private ObservableCollection<TourStep> _steps = new();
    [ObservableProperty] private TourStep? _currentStep;
    
    [ObservableProperty] private int _currentIndex = 0;
    [ObservableProperty] private string _progressText = string.Empty;

    // Computed properties to enable/disable the Next/Prev buttons
    public bool CanGoPrev => CurrentIndex > 0;
    public bool CanGoNext => CurrentIndex < Steps.Count - 1;

    // Call this from your main ViewModels to launch the tour
    public void StartTour(params TourStep[] newSteps)
    {
        Steps.Clear();
        foreach (var step in newSteps) Steps.Add(step);

        if (Steps.Count > 0)
        {
            CurrentIndex = 0;
            UpdateState();
            IsOpen = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevStep()
    {
        CurrentIndex--;
        UpdateState();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextStep()
    {
        CurrentIndex++;
        UpdateState();
    }

    [RelayCommand]
    private void CloseTour()
    {
        IsOpen = false;
    }

    private void UpdateState()
    {
      CurrentStep = Steps[CurrentIndex];
      ProgressText = $"Step {CurrentIndex + 1} of {Steps.Count}";
        
      PrevStepCommand.NotifyCanExecuteChanged();
      NextStepCommand.NotifyCanExecuteChanged();

      OnPropertyChanged(nameof(CanGoPrev));
      OnPropertyChanged(nameof(CanGoNext)); 
    }
}