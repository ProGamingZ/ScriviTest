using System.Collections.Generic;

namespace ScriviTest.DTOs;

public class AnswerKeyExamDto
{
    public string ExamId { get; set; } = string.Empty; // To match the key to the specific test later
    public List<AnswerKeySectionDto> Sections { get; set; } = new();
}

public class AnswerKeySectionDto
{
    public List<AnswerKeyQuestionDto> Questions { get; set; } = new();
}

public class AnswerKeyQuestionDto
{
    public string Type { get; set; } = string.Empty;
    public int Points { get; set; }
    
    // For Multiple Answer
    public string MultipleAnswerRubric { get; set; } = string.Empty;
    
    // For True/False
    public bool? TrueFalseCorrectAnswer { get; set; }
    
    // For Multiple Choice & Multiple Answer (Stores the indices of the correct options, e.g., [0, 2] means Option 1 and 3 are correct)
    public List<int> CorrectChoiceIndices { get; set; } = new(); 
}