using CommunityToolkit.Mvvm.ComponentModel;

namespace ScriviTest.Models;

public partial class GradeReport : ObservableObject
{
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _studentID = string.Empty;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(DisplayScore))]
    private double _totalPointsEarned;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(DisplayScore))]
    private int _maxPossiblePoints;

    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(DisplayScore))]
    private bool _requiresManualReview;

    // Always shows score. Appends a (!) if there is an ungraded essay.
    public string DisplayScore => RequiresManualReview ? $"{TotalPointsEarned} / {MaxPossiblePoints} (!)" : $"{TotalPointsEarned} / {MaxPossiblePoints}";

    // CRITICAL: We keep the raw student answers attached here so the Middle Panel can read them!
    public ScriviTest.DTOs.StudentSubmissionDto? SubmissionData { get; set; } 

    //Remembers exact location of file to overwrite!
    public string FilePath { get; set; } = string.Empty;
}