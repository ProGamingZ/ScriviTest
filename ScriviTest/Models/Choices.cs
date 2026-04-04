using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.Models;

public partial class Choice : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isCorrect = false;
}