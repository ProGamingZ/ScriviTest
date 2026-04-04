using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ScriviTest.Models;

public partial class TestSection : ObservableObject
{
    [ObservableProperty]
    private string _title = "New Section";

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private bool _shuffleQuestions = false;

    public ObservableCollection<Question> Questions { get; } = new();
}