using System.Collections.Generic;

namespace ScriviTest.DTOs;

public class AnswerKeyExamDto
{
    public string ExamId { get; set; } = string.Empty; 
    public List<AnswerKeySectionDto> Sections { get; set; } = new();
}

public class AnswerKeySectionDto
{
    public string Title { get; set; } = string.Empty;
    public List<AnswerKeyQuestionDto> Questions { get; set; } = new();
}

public class AnswerKeyQuestionDto
{
    public string Prompt { get; set; } = string.Empty; 
    public string Type { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? AttachedImageFileName { get; set; }
    
    public string MultipleAnswerRubric { get; set; } = string.Empty;
    public bool? TrueFalseCorrectAnswer { get; set; }
    public List<int> CorrectChoiceIndices { get; set; } = new(); 
    
    public List<AnswerKeyChoiceDto> Choices { get; set; } = new(); 
}


public class AnswerKeyChoiceDto
{
    public string Text { get; set; } = string.Empty;
    public string? AttachedImageFileName { get; set; }
}