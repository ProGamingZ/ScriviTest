using System.Collections.Generic;

namespace ScriviTest.Models;

public class ReviewQuestion
{
    public int QuestionNumber { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; 

    public bool IsEssay => Type == "Essay";
    public bool IsObjective => Type != "Essay";
    public double PointsAwarded { get; set; }
    public int MaxPoints { get; set; }
    
    public string EssayResponse { get; set; } = string.Empty;
    public List<ReviewChoice> Choices { get; set; } = new();
}

public class ReviewChoice
{
    public string Text { get; set; } = string.Empty;
    public bool IsStudentSelected { get; set; }
    public bool IsCorrectAnswer { get; set; }

    // This perfectly matches your wireframe request!
    public string Icon => (IsStudentSelected, IsCorrectAnswer) switch
    {
        (true, true) => "✔️", // Correct answer chosen
        (true, false) => "❌", // Wrong answer chosen
        (false, true) => "❓", // Correct answer NOT chosen
        _ => "⬜"              // Neutral (Not correct, not chosen)
    };
    
    public string IconColor => (IsStudentSelected, IsCorrectAnswer) switch
    {
        (true, true) => "#8BC34A", // Green
        (true, false) => "#F44336", // Red
        (false, true) => "#2196F3", // Blue
        _ => "LightGray"
    };
}