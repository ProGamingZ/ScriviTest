using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.Models;

public partial class Choice : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isCorrect = false;

    // --- NEW: Image Support for Choices ---
    public string? AttachedImageFullPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private string? _attachedImageFileName; 

    public bool HasImage => !string.IsNullOrEmpty(AttachedImageFileName);
}