using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;

    public ExamCreationViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateAction(new ExaminerHubViewModel(_navigateAction));
    }
}