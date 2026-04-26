using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.DTOs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ScriviTest.ViewModels.Examinee;

// 1. The UI Wrapper to track states for the Navigation Grid safely
public partial class ExamineeQuestionWrapper : ObservableObject
{
    public StudentQuestionDto Question { get; }
    public int DisplayNumber { get; }

    [ObservableProperty] private bool _isFlagged;
    [ObservableProperty] private bool _isActive;
    
    // UI Bindings for the Grid
    [ObservableProperty] private string _gridIcon = "";
    [ObservableProperty] private string _gridBackgroundColor = "#E0E0E0"; // Default Gray
    [ObservableProperty] private Avalonia.Thickness _gridBorderThickness = new(1);

    public ExamineeQuestionWrapper(StudentQuestionDto question, int num)
    {
        Question = question;
        DisplayNumber = num;
        RefreshState();
    }

    public void RefreshState()
    {
        bool isAnswered = Question.IsEssay 
            ? !string.IsNullOrWhiteSpace(Question.StudentEssayResponse) 
            : Question.Choices.Any(c => c.IsSelected);

        if (IsFlagged)
        {
            GridIcon = "⚑";
            GridBackgroundColor = "#FFC107"; // Yellow
        }
        else if (isAnswered)
        {
            GridIcon = "✓";
            GridBackgroundColor = "#4CAF50"; // Green
        }
        else
        {
            GridIcon = "";
            GridBackgroundColor = "#E0E0E0"; // Gray
        }

        // Active question gets a thick black border
        GridBorderThickness = IsActive ? new Avalonia.Thickness(3) : new Avalonia.Thickness(1);
    }
}

public partial class ExamineeTestViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.CryptographyService _cryptoService;
    private readonly string _whiteboardKey;
    private readonly string _firstName, _middleName, _lastName, _suffix, _studentID;
    
    [ObservableProperty] private StudentExamDto _examData;
    [ObservableProperty] private string _imageDirectory;

    // --- NEW: Hardware Timer ---
    [ObservableProperty] private string _timeRemainingDisplay = string.Empty;
    private readonly Stopwatch _hardwareTimer = new();
    private readonly TimeSpan _totalTimeLimit;
    private DispatcherTimer? _uiPollTimer;

    // --- NEW: Pagination & Grid State ---
    [ObservableProperty] private ObservableCollection<ExamineeQuestionWrapper> _allQuestions = new();
    [ObservableProperty] private ExamineeQuestionWrapper? _currentQuestion;
    private int _currentIndex = 0;

    // --- NEW: Lightbox State ---
    [ObservableProperty] private bool _isLightboxOpen = false;
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _lightboxImage;

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

        // 2. Hardware Timer Setup
        _totalTimeLimit = TimeSpan.FromMinutes(ExamData.TimeLimitMinutes);
        _hardwareTimer.Start();
        
        _uiPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiPollTimer.Tick += UiPollTimer_Tick;
        _uiPollTimer.Start();
        UpdateTimeDisplay();

        // 3. Flatten Questions & Load Images
        int qNum = 1;
        foreach (var section in ExamData.Sections)
        {
            foreach (var q in section.Questions)
            {
                if (!string.IsNullOrEmpty(q.AttachedImageFileName))
                {
                    string fullPath = Path.Combine(ImageDirectory, q.AttachedImageFileName);
                    if (File.Exists(fullPath)) q.ImageBitmap = new Avalonia.Media.Imaging.Bitmap(fullPath);
                }

                foreach (var c in q.Choices)
                {
                    if (!string.IsNullOrEmpty(c.AttachedImageFileName))
                    {
                        string fullPath = Path.Combine(ImageDirectory, c.AttachedImageFileName);
                        if (File.Exists(fullPath)) c.ImageBitmap = new Avalonia.Media.Imaging.Bitmap(fullPath);
                    }
                }

                AllQuestions.Add(new ExamineeQuestionWrapper(q, qNum++));
            }
        }

        // Initialize First Question
        if (AllQuestions.Count > 0)
        {
            CurrentQuestion = AllQuestions[0];
            CurrentQuestion.IsActive = true;
            CurrentQuestion.RefreshState();
            UpdateNavigationCommands();
        }
    }

    private void UiPollTimer_Tick(object? sender, EventArgs e)
    {
        var remaining = _totalTimeLimit - _hardwareTimer.Elapsed;
        if (remaining.TotalSeconds <= 0)
        {
            _uiPollTimer?.Stop();
            _hardwareTimer.Stop();
            SubmitExam();
        }
        else
        {
            TimeRemainingDisplay = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }

    private void UpdateTimeDisplay() => TimeRemainingDisplay = $"{_totalTimeLimit.Hours:D2}:{_totalTimeLimit.Minutes:D2}:{_totalTimeLimit.Seconds:D2}";

    // --- NAVIGATION COMMANDS ---
    
    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevQuestion() => JumpToQuestion(AllQuestions[_currentIndex - 1]);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextQuestion() => JumpToQuestion(AllQuestions[_currentIndex + 1]);

    private bool CanGoPrev => _currentIndex > 0;
    private bool CanGoNext => _currentIndex < AllQuestions.Count - 1;

    [RelayCommand]
    private void JumpToQuestion(ExamineeQuestionWrapper target)
    {
        if (target == null || CurrentQuestion == target) return;

        // Clean up outgoing question state
        if (CurrentQuestion != null)
        {
            CurrentQuestion.IsActive = false;
            CurrentQuestion.RefreshState(); // Triggers ✓ calculation
        }

        // Set incoming question state
        _currentIndex = AllQuestions.IndexOf(target);
        CurrentQuestion = target;
        CurrentQuestion.IsActive = true;
        CurrentQuestion.RefreshState();

        UpdateNavigationCommands();
    }

    [RelayCommand]
    private void ToggleFlag()
    {
        if (CurrentQuestion != null)
        {
            CurrentQuestion.IsFlagged = !CurrentQuestion.IsFlagged;
            CurrentQuestion.RefreshState();
        }
    }

    private void UpdateNavigationCommands()
    {
        PrevQuestionCommand.NotifyCanExecuteChanged();
        NextQuestionCommand.NotifyCanExecuteChanged();
    }

    // --- LIGHTBOX COMMANDS ---
    [RelayCommand]
    private void OpenLightbox(Avalonia.Media.Imaging.Bitmap bmp)
    {
        if (bmp == null) return;
        LightboxImage = bmp;
        IsLightboxOpen = true;
    }

    [RelayCommand]
    private void CloseLightbox()
    {
        IsLightboxOpen = false;
        LightboxImage = null;
    }

    // --- SUBMISSION ---
    [RelayCommand]
    private void SubmitExam()
    {
        _uiPollTimer?.Stop();
        _hardwareTimer.Stop();

        TimeSpan takenSpan = _hardwareTimer.Elapsed;
        TimeSpan totalSpan = _totalTimeLimit;

        string takenStr = takenSpan.Hours > 0 ? $"{takenSpan.Hours:D2}:{takenSpan.Minutes:D2}:{takenSpan.Seconds:D2}" : $"{takenSpan.Minutes:D2}:{takenSpan.Seconds:D2}";
        string totalStr = totalSpan.Hours > 0 ? $"{totalSpan.Hours:D2}:{totalSpan.Minutes:D2}:{totalSpan.Seconds:D2}" : $"{totalSpan.Minutes:D2}:{totalSpan.Seconds:D2}";
        
        var submission = new StudentSubmissionDto 
        { 
            FirstName = _firstName, MiddleName = _middleName, LastName = _lastName, Suffix = _suffix, StudentID = _studentID, 
            ExamTitle = ExamData.Title, TimeTakenDisplay = $"{takenStr} / {totalStr}"
        };

        foreach (var section in ExamData.Sections)
        {
            var subSection = new SubmissionSectionDto();
            foreach (var question in section.Questions)
            {
                var subQuestion = new SubmissionQuestionDto { EssayResponse = question.StudentEssayResponse ?? string.Empty };
                for (int i = 0; i < question.Choices.Count; i++)
                {
                    if (question.Choices[i].IsSelected) subQuestion.SelectedChoiceIndices.Add(i);
                }
                subSection.Questions.Add(subQuestion);
            }
            submission.Sections.Add(subSection);
        }

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string safeName = $"{_studentID}_{_lastName}".Replace(" ", "_");
        string xansPath = Path.Combine(desktopPath, $"{safeName}_Submission.xans");

        string jsonContent = JsonSerializer.Serialize(submission, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(xansPath, jsonContent);
        _cryptoService.EncryptFile(xansPath, _whiteboardKey);

        if (Directory.Exists(ImageDirectory)) Directory.Delete(ImageDirectory, true);

        Console.WriteLine($"Successfully exported encrypted answers to: {xansPath}");
        _navigateAction(new HomeViewModel(_navigateAction));
    }
}