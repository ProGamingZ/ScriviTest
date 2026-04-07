using Avalonia.Threading;
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
    private string _timeRemainingDisplay = string.Empty;

    private TimeSpan _timeRemaining;
    private DispatcherTimer? _examTimer;

    [ObservableProperty]
    private StudentExamDto _examData;

    [ObservableProperty]
    private string _imageDirectory;

    private readonly string _firstName, _middleName, _lastName, _suffix, _studentID;

    public ExamineeTestViewModel(Action<ViewModelBase> navigateAction, StudentExamDto decryptedExam, string tempDirectory, string whiteboardKey, string firstName, string middleName, string lastName, string suffix, string studentID)
    {
        _navigateAction = navigateAction;
        _cryptoService = new Services.CryptographyService();
        
        ExamData = decryptedExam;
        ImageDirectory = tempDirectory;
        _whiteboardKey = whiteboardKey;
        _firstName = firstName;
        _middleName = middleName;
        _lastName = lastName;
        _suffix = suffix;
        _studentID = studentID;

        _timeRemaining = TimeSpan.FromMinutes(ExamData.TimeLimitMinutes);
        UpdateTimeDisplay();

        _examTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _examTimer.Tick += Timer_Tick;
        _examTimer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_timeRemaining.TotalSeconds > 0)
        {
            _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
            UpdateTimeDisplay();
        }
        else
        {
            _examTimer?.Stop();
            // Force submit when time is up!
            SubmitExam();
        }
    }

    private void UpdateTimeDisplay()
    {
        TimeRemainingDisplay = $"{_timeRemaining.Hours:D2}:{_timeRemaining.Minutes:D2}:{_timeRemaining.Seconds:D2}";
    }

    [RelayCommand]
    private void SubmitExam()
    {
        // 2. Map the UI data to the lightweight Scantron DTO
        var submission = new StudentSubmissionDto 
        { 
            FirstName = _firstName,
            MiddleName = _middleName,
            LastName = _lastName,
            Suffix = _suffix,
            StudentID = _studentID, 
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
        string safeName = $"{_studentID}_{_lastName}".Replace(" ", "_");
        string xansPath = Path.Combine(desktopPath, $"{safeName}_Submission.xans");

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