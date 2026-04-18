using System.Collections.Generic;

namespace ScriviTest.DTOs;

public class StudentSubmissionDto
{
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty; 
    public string Suffix { get; set; } = string.Empty;
    public string StudentID { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string TimeTakenDisplay { get; set; } = string.Empty;
    public List<SubmissionSectionDto> Sections { get; set; } = new();
}

public class SubmissionSectionDto
{
    public List<SubmissionQuestionDto> Questions { get; set; } = new();
}

public class SubmissionQuestionDto
{
    //[0, 2] = checked Option 1 and Option 3
    public List<int> SelectedChoiceIndices { get; set; } = new();
    
    //essay questions
    public string EssayResponse { get; set; } = string.Empty;
    //Save grades to be edited by examiner
    public double? AwardedPoints { get; set; }
}