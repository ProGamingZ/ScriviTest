using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        // Initialize the Home Screen and give it the ability to change the CurrentPage
        _currentPage = new HomeViewModel(Navigate);
    }

    private void Navigate(ViewModelBase viewModel)
    {
        CurrentPage = viewModel;
    }

}