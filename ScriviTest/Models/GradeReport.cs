namespace ScriviTest.Models;

public class GradeReport
{
    // Properties for the DataGrid Columns
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string StudentID { get; set; } = string.Empty;

    public double TotalPointsEarned { get; set; }
    public int MaxPossiblePoints { get; set; }
    public bool RequiresManualReview { get; set; }

    // What actually shows up in the "Scores" column
    public string DisplayScore => RequiresManualReview ? "Pending Review" : $"{TotalPointsEarned} / {MaxPossiblePoints}";

    // CRITICAL: We keep the raw student answers attached here so the Middle Panel can read them!
    public ScriviTest.DTOs.StudentSubmissionDto? SubmissionData { get; set; } 
}