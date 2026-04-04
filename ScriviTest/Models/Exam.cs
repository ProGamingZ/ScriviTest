using System;
using System.Collections.ObjectModel;

namespace ScriviTest.Models;

public class Exam
{
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public string Teacher { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    
    // Anti-Cheat strictness: e.g., "Strict", "Lenient", "Log Only"
    public string AntiCheatStrictness { get; set; } = "Strict"; 

    // We will populate this later when we build the Section and Question models
    // public ObservableCollection<TestSection> Sections { get; set; } = new();
}