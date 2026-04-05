namespace ScriviTest.Models;

public class GradeReport
{
    public string StudentName { get; set; } = string.Empty;
    public double TotalPointsEarned { get; set; }
    public int MaxPossiblePoints { get; set; }
    
    // We use this to flag if the test had an essay that the teacher needs to read manually
    public bool RequiresManualReview { get; set; } 
    public string Status => RequiresManualReview ? "Needs Manual Review (Essay)" : "Auto-Graded";
}