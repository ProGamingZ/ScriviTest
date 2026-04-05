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
    // This creates the .xamn file for the Examinees
    public void ExportStudentArchive(Exam examData, string outputPath)
    {
        // 1. Map the live Exam model to the secure DTO (Scrubbing the answers)
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

        // We will temporarily collect all referenced image paths so we know what to copy
        var imagePathsToZip = new HashSet<string>();

        foreach (var section in examData.Sections)
        {
            var safeSection = new StudentSectionDto { Title = section.Title, ShuffleQuestions = section.ShuffleQuestions };
            
            foreach (var question in section.Questions)
            {
                var safeQuestion = new StudentQuestionDto
                {
                    Prompt = question.Prompt,
                    Type = question.Type.ToString(),
                    Points = question.Points,
                    AttachedImageFileName = question.AttachedImageFileName,
                    MaxWordCount = question.MaxWordCount
                };

                if (!string.IsNullOrEmpty(question.AttachedImageFullPath))
                    imagePathsToZip.Add(question.AttachedImageFullPath);

                foreach (var choice in question.Choices)
                {
                    safeQuestion.Choices.Add(new StudentChoiceDto 
                    { 
                        Text = choice.Text,
                        AttachedImageFileName = choice.AttachedImageFileName
                    });

                    if (!string.IsNullOrEmpty(choice.AttachedImageFullPath))
                        imagePathsToZip.Add(choice.AttachedImageFullPath);
                }
                safeSection.Questions.Add(safeQuestion);
            }
            safeExam.Sections.Add(safeSection);
        }

        // 2. Serialize the safe DTO to JSON
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string jsonContent = JsonSerializer.Serialize(safeExam, jsonOptions);

        // 3. Create the Zip Archive (.xamn)
        using (FileStream zipToOpen = new FileStream(outputPath, FileMode.Create))
        {
            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                // Add the JSON file
                ZipArchiveEntry jsonEntry = archive.CreateEntry("exam_data.json");
                using (StreamWriter writer = new StreamWriter(jsonEntry.Open()))
                {
                    writer.Write(jsonContent);
                }

                // Add the Images into a /media folder
                foreach (var imagePath in imagePathsToZip)
                {
                    if (File.Exists(imagePath))
                    {
                        string fileName = Path.GetFileName(imagePath);
                        // The forward slash creates the folder structure inside the zip
                        archive.CreateEntryFromFile(imagePath, $"media/{fileName}");
                    }
                }
            }
        }
    }
}