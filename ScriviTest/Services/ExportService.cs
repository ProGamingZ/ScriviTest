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
    public void ExportExamPackage(Exam examData, string xamnPath, string xamkPath)
    {
        // --- 1. PREPARE THE STUDENT DTO (No Answers) ---
        var safeExam = new StudentExamDto
        {
            Title = examData.Title,
            Instructions = examData.Instructions,
            TimeLimitMinutes = examData.TimeLimitMinutes,
            Teacher = examData.Teacher,
            Subject = examData.Subject,
            Section = examData.Section,
            AntiCheatStrictness = examData.AntiCheatStrictness
        };

        // --- 2. PREPARE THE ANSWER KEY DTO ---
        var answerKey = new AnswerKeyExamDto
        {
            ExamId = Guid.NewGuid().ToString() // Unique ID to link the Test to the Key
        };

        var imagePathsToZip = new HashSet<string>();

        foreach (var section in examData.Sections)
        {
            var safeSection = new StudentSectionDto { Title = section.Title, ShuffleQuestions = section.ShuffleQuestions };
            var keySection = new AnswerKeySectionDto();
            
            foreach (var question in section.Questions)
            {
                // Build the Student Question
                var safeQuestion = new StudentQuestionDto
                {
                    Prompt = question.Prompt,
                    Type = question.Type.ToString(),
                    Points = question.Points,
                    AttachedImageFileName = question.AttachedImageFileName,
                    MaxWordCount = question.MaxWordCount
                };

                // Build the Answer Key Question
                var keyQuestion = new AnswerKeyQuestionDto
                {
                    Type = question.Type.ToString(),
                    Points = question.Points,
                    MultipleAnswerRubric = question.MultipleAnswerRubric.ToString(),
                    TrueFalseCorrectAnswer = question.Type == QuestionType.TrueFalse ? question.IsTrueFalseAnswerTrue : null
                };

                if (!string.IsNullOrEmpty(question.AttachedImageFullPath))
                    imagePathsToZip.Add(question.AttachedImageFullPath);

                // Loop through choices to populate BOTH DTOs
                for (int i = 0; i < question.Choices.Count; i++)
                {
                    var choice = question.Choices[i];

                    // Give student the text/image, but NOT the IsCorrect boolean
                    safeQuestion.Choices.Add(new StudentChoiceDto 
                    { 
                        Text = choice.Text,
                        AttachedImageFileName = choice.AttachedImageFileName
                    });

                    // If it's correct, record the index number in the Answer Key!
                    if (choice.IsCorrect)
                    {
                        keyQuestion.CorrectChoiceIndices.Add(i);
                    }

                    if (!string.IsNullOrEmpty(choice.AttachedImageFullPath))
                        imagePathsToZip.Add(choice.AttachedImageFullPath);
                }
                
                safeSection.Questions.Add(safeQuestion);
                keySection.Questions.Add(keyQuestion);
            }
            safeExam.Sections.Add(safeSection);
            answerKey.Sections.Add(keySection);
        }

        // --- 3. GENERATE THE .XAMN STUDENT ARCHIVE ---
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string studentJson = JsonSerializer.Serialize(safeExam, jsonOptions);

        using (FileStream zipToOpen = new FileStream(xamnPath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                ZipArchiveEntry jsonEntry = archive.CreateEntry("exam_data.json");
                using (StreamWriter writer = new StreamWriter(jsonEntry.Open()))
                {
                    writer.Write(studentJson);
                }

                foreach (var imagePath in imagePathsToZip)
                {
                    if (File.Exists(imagePath))
                    {
                        archive.CreateEntryFromFile(imagePath, $"media/{Path.GetFileName(imagePath)}");
                    }
                }
            }
        }

        // --- 4. GENERATE THE .XAMK ANSWER KEY ---
        string keyJson = JsonSerializer.Serialize(answerKey, jsonOptions);
        File.WriteAllText(xamkPath, keyJson);
    }

    // Generates an Excel-ready CSV Grade Report
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