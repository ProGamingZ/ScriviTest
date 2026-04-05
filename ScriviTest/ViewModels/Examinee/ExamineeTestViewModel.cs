using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.DTOs;
using System;
using System.IO;
using System.Text.Json;

namespace ScriviTest.ViewModels.Examinee;

public partial class ExamineeTestViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.CryptographyService _cryptoService;
    private readonly string _whiteboardKey;

    [ObservableProperty]
    private StudentExamDto _examData;

    [ObservableProperty]
    private string _imageDirectory;

    // NEW: We need to ask the student for their name!
    [ObservableProperty]
    private string _studentName = string.Empty;

    public ExamineeTestViewModel(Action<ViewModelBase> navigateAction, StudentExamDto decryptedExam, string tempDirectory, string whiteboardKey)
    {
        _navigateAction = navigateAction;
        _cryptoService = new Services.CryptographyService();
        
        ExamData = decryptedExam;
        ImageDirectory = tempDirectory;
        _whiteboardKey = whiteboardKey;
    }

    [RelayCommand]
    private void SubmitExam()
    {
        // 1. Ensure they typed a name
        if (string.IsNullOrWhiteSpace(StudentName))
        {
            Console.WriteLine("Error: Student must enter their name!");
            return; 
        }

        // 2. Map the UI data to the lightweight Scantron DTO
        var submission = new StudentSubmissionDto 
        { 
            StudentName = this.StudentName, 
            ExamTitle = ExamData.Title 
        };

        foreach (var section in ExamData.Sections)
        {
            var subSection = new SubmissionSectionDto();
            foreach (var question in section.Questions)
            {
                var subQuestion = new SubmissionQuestionDto 
                { 
                    EssayResponse = question.StudentEssayResponse ?? string.Empty 
                };

                for (int i = 0; i < question.Choices.Count; i++)
                {
                    if (question.Choices[i].IsSelected)
                    {
                        subQuestion.SelectedChoiceIndices.Add(i);
                    }
                }
                subSection.Questions.Add(subQuestion);
            }
            submission.Sections.Add(subSection);
        }

        // 3. Serialize to JSON and save as .xans on the Desktop
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string safeStudentName = StudentName.Replace(" ", "_");
        string xansPath = Path.Combine(desktopPath, $"{safeStudentName}_Submission.xans");

        string jsonContent = JsonSerializer.Serialize(submission, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(xansPath, jsonContent);

        // 4. Encrypt the Answer File using the Whiteboard Key
        _cryptoService.EncryptFile(xansPath, _whiteboardKey);

        // 5. SELF DESTRUCT: Wipe the decrypted images from RAM/Temp storage to prevent cheating
        if (Directory.Exists(ImageDirectory))
        {
            Directory.Delete(ImageDirectory, true);
        }

        // 6. Route the user back to the Home Screen (This also instantly breaks them out of Fullscreen lockdown!)
        Console.WriteLine($"Successfully exported encrypted answers to: {xansPath}");
        _navigateAction(new HomeViewModel(_navigateAction));
    }
}