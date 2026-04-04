using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.Models;

// Changed to ObservableObject so the UI can bind to the global settings!
public partial class Exam : ObservableObject 
{
    [ObservableProperty]
    private string _title = "Untitled Exam";
    
    [ObservableProperty]
    private string _instructions = string.Empty;
    
    [ObservableProperty]
    private int _timeLimitMinutes = 60;
    
    [ObservableProperty]
    private string _teacher = string.Empty;
    
    [ObservableProperty]
    private string _subject = string.Empty;
    
    [ObservableProperty]
    private string _section = string.Empty;
    
    [ObservableProperty]
    private string _antiCheatStrictness = "Strict"; 

    // The root collection containing all sections and questions
    public ObservableCollection<TestSection> Sections { get; } = new();
}