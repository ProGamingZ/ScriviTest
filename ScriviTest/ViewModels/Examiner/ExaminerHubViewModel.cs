using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace ScriviTest.ViewModels.Examiner;

public partial class ExaminerHubViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;

    // We pass in a navigation action so this ViewModel can tell the MainWindow to change pages
    public ExaminerHubViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
    }

    [RelayCommand]
    private void NavigateToCreation()
    {
        // TODO: _navigateAction(new ExamCreationViewModel());
        _navigateAction(new ExamCreationViewModel(_navigateAction));
    }

    [RelayCommand]
    private void NavigateToGrading()
    {
        // TODO: _navigateAction(new GradingDashboardViewModel());
        Console.WriteLine("Routing to Grading Dashboard...");
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new HomeViewModel(_navigateAction));
    }
}