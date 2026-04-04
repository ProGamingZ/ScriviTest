using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage = new HomeViewModel();
}