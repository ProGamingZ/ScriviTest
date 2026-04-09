using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using ScriviTest.Models;
using ScriviTest.DTOs;

namespace ScriviTest.Services;

public class ExportService
{
    // Now takes both output paths!
    public void ExportExamPackage(Models.Exam rawExam, string xamnPath, string xamkPath)
    {
        // 1. Create the Answer Key DTO (.xamk)
        var answerKey = new DTOs.AnswerKeyExamDto { ExamId = Guid.NewGuid().ToString() };
        
        // 2. Create the Student Exam DTO (.xamn)
        var studentExam = new DTOs.StudentExamDto
        {
            Title = rawExam.Title,
            Instructions = rawExam.Instructions,
            TimeLimitMinutes = rawExam.TimeLimitMinutes,
            Teacher = rawExam.Teacher,
            Subject = rawExam.Subject,
            Section = rawExam.Section,
            AntiCheatStrictness = rawExam.AntiCheatStrictness
        };

        foreach (var section in rawExam.Sections)
        {
            var keySection = new DTOs.AnswerKeySectionDto();
            var studentSection = new DTOs.StudentSectionDto 
            { 
                Title = section.Title,
                ShuffleQuestions = section.ShuffleQuestions
            };

            foreach (var question in section.Questions)
            {
                // -- BUILD THE ANSWER KEY QUESTION --
                var keyQuestion = new DTOs.AnswerKeyQuestionDto
                {
                    Prompt = question.Prompt, 
                    Type = question.Type.ToString(),
                    Points = question.Points,
                    MultipleAnswerRubric = question.MultipleAnswerRubric.ToString(),
                    AttachedImageFileName = question.AttachedImageFileName
                };

                // -- BUILD THE STUDENT QUESTION --
                var studentQuestion = new DTOs.StudentQuestionDto
                {
                    Prompt = question.Prompt, 
                    Type = question.Type.ToString(),
                    Points = question.Points,
                    MaxWordCount = question.MaxWordCount,
                    AttachedImageFileName = question.AttachedImageFileName
                };

                // -- FIX: MAP THE CHOICES PROPERLY --
                if (question.Type == Models.QuestionType.TrueFalse)
                {
                    // Manually build True/False choices so the UI has text to display
                    keyQuestion.Choices.Add(new DTOs.AnswerKeyChoiceDto { Text = "True" });
                    keyQuestion.Choices.Add(new DTOs.AnswerKeyChoiceDto { Text = "False" });
                    
                    studentQuestion.Choices.Add(new DTOs.StudentChoiceDto { Text = "True" });
                    studentQuestion.Choices.Add(new DTOs.StudentChoiceDto { Text = "False" });

                    if (question.IsTrueFalseAnswerTrue)
                        keyQuestion.CorrectChoiceIndices.Add(0); // 0 is True
                    else
                        keyQuestion.CorrectChoiceIndices.Add(1); // 1 is False
                }
                else if (question.Type != Models.QuestionType.Essay)
                {
                    // Map Multiple Choice & Multiple Answer Options
                    for (int i = 0; i < question.Choices.Count; i++)
                    {
                        var choice = question.Choices[i];
                        
                        keyQuestion.Choices.Add(new DTOs.AnswerKeyChoiceDto { Text = choice.Text });
                        studentQuestion.Choices.Add(new DTOs.StudentChoiceDto 
                        { 
                            Text = choice.Text,
                            AttachedImageFileName = choice.AttachedImageFileName
                        });

                        if (choice.IsCorrect)
                        {
                            keyQuestion.CorrectChoiceIndices.Add(i);
                        }
                    }
                }

                keySection.Questions.Add(keyQuestion);
                studentSection.Questions.Add(studentQuestion);
            }
            answerKey.Sections.Add(keySection);
            studentExam.Sections.Add(studentSection);
        }

        // 3. Serialize and save the Answer Key (.xamk)
        string keyJson = JsonSerializer.Serialize(answerKey, new JsonSerializerOptions { WriteIndented = true });
        
        using (var archive = System.IO.Compression.ZipFile.Open(xamkPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var jsonEntry = archive.CreateEntry("answer_key.json");
            using (var writer = new StreamWriter(jsonEntry.Open())) { writer.Write(keyJson); }

            // Copy images into the .xamk Zip
            foreach (var section in rawExam.Sections)
            {
                foreach (var question in section.Questions)
                {
                    if (question.HasImage && File.Exists(question.AttachedImageFullPath))
                        archive.CreateEntryFromFile(question.AttachedImageFullPath, $"media/{question.AttachedImageFileName}");
                    
                    foreach (var choice in question.Choices)
                    {
                        if (choice.HasImage && File.Exists(choice.AttachedImageFullPath))
                            archive.CreateEntryFromFile(choice.AttachedImageFullPath, $"media/{choice.AttachedImageFileName}");
                    }
                }
            }
        }

        // 4. Serialize and Package the Student Exam (.xamn)
        string studentJson = JsonSerializer.Serialize(studentExam, new JsonSerializerOptions { WriteIndented = true });
        
        using (var archive = System.IO.Compression.ZipFile.Open(xamnPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var jsonEntry = archive.CreateEntry("exam_data.json");
            using (var writer = new StreamWriter(jsonEntry.Open()))
            {
                writer.Write(studentJson);
            }

            // Copy images into the Zip
            foreach (var section in rawExam.Sections)
            {
                foreach (var question in section.Questions)
                {
                    if (question.HasImage && File.Exists(question.AttachedImageFullPath))
                    {
                        archive.CreateEntryFromFile(question.AttachedImageFullPath, $"media/{question.AttachedImageFileName}");
                    }
                    foreach (var choice in question.Choices)
                    {
                        if (choice.HasImage && File.Exists(choice.AttachedImageFullPath))
                        {
                            archive.CreateEntryFromFile(choice.AttachedImageFullPath, $"media/{choice.AttachedImageFileName}");
                        }
                    }
                }
            }
        }

    }

    // Generates an Excel-ready CSV Grade Report
    public void ExportGradeReportToCsv(List<Models.GradeReport> grades, string outputPath)
    {
        var csv = new System.Text.StringBuilder();
        
        // 1. Write the updated Header Row
        csv.AppendLine("First Name,Middle Name,Last Name,Student ID,Total Points Earned,Max Possible Points,Review Status");

        // 2. Loop through the grades and format each row
        foreach (var grade in grades)
        {
            // Security: Wrap text fields in quotes to prevent rogue commas from breaking the CSV layout
            string safeFirst = $"\"{grade.FirstName.Replace("\"", "\"\"")}\"";
            string safeMiddle = $"\"{grade.MiddleName.Replace("\"", "\"\"")}\"";
            string safeLast = $"\"{grade.LastName.Replace("\"", "\"\"")}\"";
            string safeID = $"\"{grade.StudentID.Replace("\"", "\"\"")}\"";
            
            // Recreate the status string
            string status = grade.RequiresManualReview ? "Needs Manual Review (Essay)" : "Auto-Graded";

            // Append the row!
            csv.AppendLine($"{safeFirst},{safeMiddle},{safeLast},{safeID},{grade.TotalPointsEarned},{grade.MaxPossiblePoints},{status}");
        }

        // 3. Write to the hard drive
        File.WriteAllText(outputPath, csv.ToString());
    }
}