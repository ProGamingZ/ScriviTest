using System.Collections.Generic;

namespace ScriviTest.DTOs;

public class StudentExamDto
{
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public int TimeLimitMinutes { get; set; }
    public string Teacher { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string AntiCheatStrictness { get; set; } = string.Empty;
    public List<StudentSectionDto> Sections { get; set; } = new();
}

public class StudentSectionDto
{
    public string Title { get; set; } = string.Empty;
    public bool ShuffleQuestions { get; set; }
    public List<StudentQuestionDto> Questions { get; set; } = new();
}

public class StudentQuestionDto
{
    public string Prompt { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; 
    public int Points { get; set; }
    public string? AttachedImageFileName { get; set; }
    public int MaxWordCount { get; set; }
    public List<StudentChoiceDto> Choices { get; set; } = new();
    public string StudentEssayResponse { get; set; } = string.Empty;
}

public class StudentChoiceDto
{
    public string Text { get; set; } = string.Empty;
    public string? AttachedImageFileName { get; set; }
    public bool IsSelected { get; set; } = false;
}