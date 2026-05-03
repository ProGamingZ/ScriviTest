using System.Text;
using ScriviTest.Models;

namespace ScriviTest.Services;

public static class HtmlReportGenerator
{
    public static string GenerateStudentReport(GradeReport report, DTOs.AnswerKeyExamDto answerKey)
    {
        var sb = new StringBuilder();

        // 1. Setup HTML and CSS Styling
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'><head><meta charset='UTF-8'>");
        sb.AppendLine($"<title>{report.LastName}, {report.FirstName} - Exam Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("  body { font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; max-width: 900px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine("  .header { border-bottom: 3px solid #1976D2; padding-bottom: 20px; margin-bottom: 30px; }");
        sb.AppendLine("  .score-box { float: right; background: #f0f8ff; padding: 15px 25px; border-radius: 8px; border: 1px solid #cce0ff; text-align: center; }");
        sb.AppendLine("  .score-number { font-size: 28px; font-weight: bold; color: #1976D2; }");
        sb.AppendLine("  .question-container { margin-bottom: 30px; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; page-break-inside: avoid; }");
        sb.AppendLine("  .q-header { font-weight: bold; font-size: 18px; margin-bottom: 10px; }");
        sb.AppendLine("  .remarks-box { background: #fffde7; border-left: 4px solid #fbc02d; padding: 10px 15px; margin-top: 15px; font-style: italic; }");
        sb.AppendLine("  .correct-text { color: #2e7d32; font-weight: bold; }");
        sb.AppendLine("  .incorrect-text { color: #c62828; font-weight: bold; }");
        sb.AppendLine("  .essay-box { background: #f5f5f5; padding: 15px; border-radius: 4px; border: 1px solid #ddd; white-space: pre-wrap; }");
        sb.AppendLine("  @media print { body { padding: 0; } .question-container { border: none; border-bottom: 1px solid #ccc; border-radius: 0; } }");
        sb.AppendLine("</style></head><body>");

        // 2. The Header Section
        sb.AppendLine("<div class='header'>");
        sb.AppendLine("<div class='score-box'>");
        sb.AppendLine("<div style='font-size: 14px; color: #666;'>FINAL SCORE</div>");
        sb.AppendLine($"<div class='score-number'>{report.TotalPointsEarned} / {report.MaxPossiblePoints}</div>");
        sb.AppendLine("</div>");
        sb.AppendLine($"<h1>{report.LastName}, {report.FirstName} {report.MiddleName}</h1>");
        sb.AppendLine($"<p><strong>Student ID:</strong> {report.StudentID}<br/>");
        sb.AppendLine($"<strong>Time Taken:</strong> {report.SubmissionData?.TimeTakenDisplay ?? "N/A"}</p>");
        sb.AppendLine("<div style='clear: both;'></div>");
        sb.AppendLine("</div>");

        // 3. Loop through Sections and Questions
        if (report.SubmissionData != null)
        {
            int qNumber = 1;
            for (int s = 0; s < report.SubmissionData.Sections.Count; s++)
            {
                if (s >= answerKey.Sections.Count) continue;
                var stuSection = report.SubmissionData.Sections[s];
                var keySection = answerKey.Sections[s];

                sb.AppendLine($"<h2>{keySection.Title}</h2>");

                for (int q = 0; q < stuSection.Questions.Count; q++)
                {
                    if (q >= keySection.Questions.Count) continue;
                    var stuQ = stuSection.Questions[q];
                    var keyQ = keySection.Questions[q];

                    sb.AppendLine("<div class='question-container'>");
                    
                    // Question Prompt & Points
                    sb.AppendLine($"<div class='q-header'>Q{qNumber++}. {keyQ.Prompt} <span style='float:right; font-weight:normal; color:#666;'>{stuQ.AwardedPoints ?? 0} / {keyQ.Points} pts</span></div>");

                    // Render Answers based on Type
                    if (keyQ.Type == "Essay")
                    {
                        sb.AppendLine($"<div class='essay-box'>{System.Net.WebUtility.HtmlEncode(stuQ.EssayResponse)}</div>");
                    }
                    else // Objective Questions
                    {
                        sb.AppendLine("<ul style='list-style-type: none; padding-left: 0;'>");
                        for (int c = 0; c < keyQ.Choices.Count; c++)
                        {
                            bool isStudentSelected = stuQ.SelectedChoiceIndices.Contains(c);
                            bool isCorrectAnswer = keyQ.CorrectChoiceIndices.Contains(c);
                            
                            string prefix = "[&nbsp;&nbsp;]";
                            if (isStudentSelected && isCorrectAnswer) prefix = "<span class='correct-text'>[ ✓ ]</span>";
                            else if (isStudentSelected && !isCorrectAnswer) prefix = "<span class='incorrect-text'>[ ✕ ]</span>";
                            else if (!isStudentSelected && isCorrectAnswer) prefix = "<span style='color:#1976D2; font-weight:bold;'>[ ! ]</span>"; // Should have picked this

                            sb.AppendLine($"<li style='margin-bottom: 5px;'>{prefix} {keyQ.Choices[c].Text}</li>");
                        }
                        sb.AppendLine("</ul>");
                    }

                    // Render Remarks if they exist!
                    if (!string.IsNullOrWhiteSpace(stuQ.Remarks))
                    {
                        sb.AppendLine($"<div class='remarks-box'><strong>Examiner Remarks:</strong><br/>{System.Net.WebUtility.HtmlEncode(stuQ.Remarks)}</div>");
                    }

                    sb.AppendLine("</div>"); // End Question Container
                }
            }
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}