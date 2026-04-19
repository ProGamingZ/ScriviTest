using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScriviTest.ViewModels.Examiner;

public partial class GradingHubViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;
    private readonly Services.CryptographyService _cryptoService;
    private readonly string _examinerTempImageDir = Path.Combine(Path.GetTempPath(), "ScriviTest", "ExaminerKeyMedia");
    private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _imageCache = new(); //holds images in ram

    // To store the answer key globally after unlocking
    private DTOs.AnswerKeyExamDto? _loadedAnswerKey;

    [ObservableProperty] private string _currentTimeTakenDisplay = string.Empty;
    [ObservableProperty] private ObservableCollection<Models.ReviewSection> _currentReviewSections = new();
    // Navigation for the Middle Panel
    [ObservableProperty] private ObservableCollection<Models.ReviewQuestion> _currentStudentQuestions = new();
    [ObservableProperty] private Models.ReviewQuestion? _currentVisibleQuestion;
    
    private int _currentQuestionIndex = 0;
    [ObservableProperty] private string? _answerKeyPath;
    [ObservableProperty] private string _answerKeyFileName = string.Empty;
    [ObservableProperty] private List<string> _studentSubmissionPaths = new();
    [ObservableProperty] private string _studentFilesSummary = string.Empty;
    [ObservableProperty] private string _whiteboardKey = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private double _currentTotalScore = 0;
    [ObservableProperty] private int _currentMaxScore = 0;
    
    [ObservableProperty] private ObservableCollection<Models.GradeReport> _studentList = new();
    [ObservableProperty] private Models.GradeReport? _selectedStudent;

    [ObservableProperty] private bool _showFirstName = true;
    [ObservableProperty] private bool _showMiddleName = true;
    [ObservableProperty] private bool _showLastName = true;
    [ObservableProperty] private bool _showID = true;
    [ObservableProperty] private bool _showScores = true;

    public GradingHubViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService();
        _cryptoService = new Services.CryptographyService();
    }

    [RelayCommand]
    private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));

    [RelayCommand]
    private async Task BrowseForAnswerKey()
    {
        ErrorMessage = string.Empty;
        var path = await _fileService.PickAnswerKeyAsync();
        if (!string.IsNullOrEmpty(path))
        {
            AnswerKeyPath = path;
            AnswerKeyFileName = Path.GetFileName(path);
        }
    }

    [RelayCommand]
    private async Task BrowseForStudentFiles()
    {
        ErrorMessage = string.Empty;
        var paths = await _fileService.PickStudentSubmissionsAsync();
        if (paths.Count > 0)
        {
            StudentSubmissionPaths = paths;
            StudentFilesSummary = $"{paths.Count} student submission(s) loaded.";
        }
    }

    // Validation for the Check button
    private bool CanCheckAndUnlock => !string.IsNullOrEmpty(AnswerKeyPath) && StudentSubmissionPaths.Count > 0 && WhiteboardKey.Length >= 6;

    partial void OnAnswerKeyPathChanged(string? value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();
    partial void OnStudentSubmissionPathsChanged(List<string> value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();
    partial void OnWhiteboardKeyChanged(string value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanCheckAndUnlock))]
    private void CheckAndUnlock()
    {
        ErrorMessage = string.Empty;
        StudentList.Clear(); // Clear old list if they click it twice

        try
        {
            if (Directory.Exists(_examinerTempImageDir)) Directory.Delete(_examinerTempImageDir, true);
            Directory.CreateDirectory(_examinerTempImageDir);

            using var archive = System.IO.Compression.ZipFile.OpenRead(AnswerKeyPath!);
            
            // Clear old images from previous grading sessions
            foreach (var bmp in _imageCache.Values) bmp.Dispose(); 
            _imageCache.Clear();

            // Extract Images & Cache them instantly
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("media/") && !string.IsNullOrEmpty(entry.Name))
                {
                    string destPath = Path.Combine(_examinerTempImageDir, entry.Name);
                    entry.ExtractToFile(destPath, true);
                    
                    // LOAD INTO RAM ONCE
                    _imageCache[entry.Name] = new Avalonia.Media.Imaging.Bitmap(destPath);
                }
            }

            // Read the JSON
            var jsonEntry = archive.GetEntry("answer_key.json");
            if (jsonEntry == null) throw new Exception("Invalid Answer Key Format.");
            using var reader = new StreamReader(jsonEntry.Open());
            _loadedAnswerKey = JsonSerializer.Deserialize<DTOs.AnswerKeyExamDto>(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to read Answer Key: {ex.Message}";
            return;
        }

        // 2. BATCH DECRYPT & GRADE
        if (_loadedAnswerKey == null)
        {
            ErrorMessage = "Answer key not loaded. Cannot grade submissions.";
            return;
        }
        foreach (var path in StudentSubmissionPaths)
        {
            var studentSubmission = _cryptoService.DecryptStudentSubmission(path, WhiteboardKey.ToUpper());
            if (studentSubmission == null)
            {
                ErrorMessage = $"Access Denied: Failed to decrypt {Path.GetFileName(path)}. Wrong Whiteboard Key?";
                return; 
            }

            // Create Grade Report 
            var report = new Models.GradeReport 
            { 
                FirstName = studentSubmission.FirstName,
                MiddleName = studentSubmission.MiddleName,
                LastName = studentSubmission.LastName,
                StudentID = studentSubmission.StudentID,
                SubmissionData = studentSubmission, // keep raw data Middle Panel
                FilePath = path,
                MaxPossiblePoints = 0,
                TotalPointsEarned = 0,
                RequiresManualReview = false
            };


            // calculate autograde & inject directly into DTO
            for (int s = 0; s < studentSubmission.Sections.Count; s++)
            {
                if (s >= _loadedAnswerKey.Sections.Count) continue;
                for (int q = 0; q < studentSubmission.Sections[s].Questions.Count; q++)
                {
                    if (q >= _loadedAnswerKey.Sections[s].Questions.Count) continue;
                    var studentQ = studentSubmission.Sections[s].Questions[q];
                    var keyQ = _loadedAnswerKey.Sections[s].Questions[q];

                    report.MaxPossiblePoints += keyQ.Points;

                    // if not graded yet, run Auto-Grader
                    if (!studentQ.AwardedPoints.HasValue)
                    {
                        double autoGrade = 0;
                        if (keyQ.Type == "Essay") report.RequiresManualReview = true;
                        else if (keyQ.Type == "MultipleChoice" || keyQ.Type == "TrueFalse")
                        {
                            if (studentQ.SelectedChoiceIndices.Count == 1 && keyQ.CorrectChoiceIndices.Contains(studentQ.SelectedChoiceIndices[0]))
                                autoGrade = keyQ.Points;
                        }
                        else if (keyQ.Type == "MultipleAnswer")
                        {
                            var studentAns = new HashSet<int>(studentQ.SelectedChoiceIndices);
                            var keyAns = new HashSet<int>(keyQ.CorrectChoiceIndices);
                            if (keyQ.MultipleAnswerRubric == "AllOrNothing") { if (studentAns.SetEquals(keyAns)) autoGrade = keyQ.Points; }
                            else
                            {
                                double pointsPerCorrect = keyAns.Count > 0 ? (double)keyQ.Points / keyAns.Count : 0;
                                foreach (int ans in studentAns) { if (keyAns.Contains(ans)) autoGrade += pointsPerCorrect; else autoGrade -= 1; }
                                if (autoGrade < 0) autoGrade = 0; 
                            }
                        }
                        studentQ.AwardedPoints = autoGrade; // save auto-grade into DTO!
                    }

                    report.TotalPointsEarned += studentQ.AwardedPoints.Value;
                }
            }
            // Add it to the UI List!
            StudentList.Add(report);
        }
    }

    partial void OnSelectedStudentChanged(Models.GradeReport? value)
    {
        // 1. Build brand-new lists in memory to force Avalonia to redraw the UI!
        var freshFlatList = new ObservableCollection<Models.ReviewQuestion>();
        var freshSectionList = new ObservableCollection<Models.ReviewSection>();
        
        _currentQuestionIndex = 0;
        CurrentVisibleQuestion = null;

        if (value == null || value.SubmissionData == null || _loadedAnswerKey == null)
        {
            // Wipe the UI clean if no student is selected
            CurrentStudentQuestions = freshFlatList;
            CurrentReviewSections = freshSectionList;
            CurrentTimeTakenDisplay = string.Empty;
            return;
        }

        int qNumber = 1;

        for (int s = 0; s < value.SubmissionData.Sections.Count; s++)
        {
            if (s >= _loadedAnswerKey.Sections.Count) continue;
            var studentSection = value.SubmissionData.Sections[s];
            var keySection = _loadedAnswerKey.Sections[s];

            // Create the Section Wrapper
            var reviewSection = new Models.ReviewSection();

            for (int q = 0; q < studentSection.Questions.Count; q++)
            {
                if (q >= keySection.Questions.Count) continue;
                var studentQ = studentSection.Questions[q];
                var keyQ = keySection.Questions[q];

                double earnedPoints = studentQ.AwardedPoints ?? 0;
                bool isSingleSelection = keyQ.Type == "MultipleChoice" || keyQ.Type == "TrueFalse";

                var reviewQ = new Models.ReviewQuestion
                {
                    QuestionNumber = qNumber++,
                    Prompt = keyQ.Prompt,
                    Type = keyQ.Type,
                    MaxPoints = keyQ.Points,
                    EssayResponse = studentQ.EssayResponse ?? string.Empty,
                    PointsAwarded = earnedPoints 
                };

                reviewQ.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(Models.ReviewQuestion.PointsAwarded))
                        RecalculateCurrentScore();
                };

                
                if (!string.IsNullOrEmpty(keyQ.AttachedImageFileName) && _imageCache.TryGetValue(keyQ.AttachedImageFileName, out var cachedQImg))
                {
                    reviewQ.ImageBitmap = cachedQImg;
                }

                for (int c = 0; c < keyQ.Choices.Count; c++)
                {
                    var reviewChoice = new Models.ReviewChoice
                    {
                        Text = keyQ.Choices[c].Text,
                        IsCorrectAnswer = keyQ.CorrectChoiceIndices.Contains(c),
                        IsStudentSelected = studentQ.SelectedChoiceIndices.Contains(c),
                        IsSingleSelection = isSingleSelection
                    };

                    var attachedImageFileName = keyQ.Choices[c].AttachedImageFileName;
                    if (!string.IsNullOrEmpty(attachedImageFileName) && 
                        _imageCache.TryGetValue(attachedImageFileName!, out var cachedCImg))
                    {
                        reviewChoice.ImageBitmap = cachedCImg;
                    }

                    reviewQ.Choices.Add(reviewChoice);
                }

                // Add to BOTH the Section (for the grid) and the Flat List (for Prev/Next buttons)
                reviewSection.Questions.Add(reviewQ);
                freshFlatList.Add(reviewQ);
            }
            
            // Add the finished section to our list
            if (reviewSection.Questions.Count > 0)
            {
                freshSectionList.Add(reviewSection);
            }
        }

        // SWAP THE LISTS: This triggers the instant visual update!
        CurrentStudentQuestions = freshFlatList;
        CurrentReviewSections = freshSectionList;

        if (CurrentStudentQuestions.Count > 0)
        {
            CurrentVisibleQuestion = CurrentStudentQuestions[0];
            UpdateNavigationCommands();
        }
        
        CurrentMaxScore = value.MaxPossiblePoints;
        CurrentTimeTakenDisplay = value.SubmissionData.TimeTakenDisplay;
        RecalculateCurrentScore();
    }    
    
    private void RecalculateCurrentScore()
    {
        double total = 0;
        foreach (var q in CurrentStudentQuestions) total += q.PointsAwarded;
        CurrentTotalScore = total;
    }

    [RelayCommand]
    private void JumpToQuestion(Models.ReviewQuestion question)
    {
        if (question == null) return;
        _currentQuestionIndex = CurrentStudentQuestions.IndexOf(question);
        CurrentVisibleQuestion = question;
        UpdateNavigationCommands();
    }

    [RelayCommand]
    private void FinishChecking()
    {
        if (SelectedStudent == null) return;

        // 1. Map the Examiner's UI changes back into the raw DTO
        int flatIndex = 0;
        foreach (var studentSection in SelectedStudent.SubmissionData!.Sections)
        {
            foreach (var studentQ in studentSection.Questions)
            {
                if (flatIndex < CurrentStudentQuestions.Count)
                {
                    studentQ.AwardedPoints = CurrentStudentQuestions[flatIndex].PointsAwarded;
                    flatIndex++;
                }
            }
        }

        // 2. OVERWRITE THE .XANS FILE SECURELY ON THE HARD DRIVE
        try
        {
            string jsonContent = JsonSerializer.Serialize(SelectedStudent.SubmissionData, new JsonSerializerOptions { WriteIndented = true });
            
            // Write the unencrypted JSON temporarily, then encrypt it in-place using the Whiteboard key!
            File.WriteAllText(SelectedStudent.FilePath, jsonContent);
            _cryptoService.EncryptFile(SelectedStudent.FilePath, WhiteboardKey.ToUpper());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save grades: {ex.Message}";
            return;
        }

        // 3. Update the Left Panel DataGrid
        SelectedStudent.TotalPointsEarned = CurrentTotalScore;
        SelectedStudent.RequiresManualReview = false; 

        // Force the DataGrid to visually refresh this specific row
        int index = StudentList.IndexOf(SelectedStudent);
        StudentList[index] = SelectedStudent; 

        // 4. Auto-advance to the next student
        if (index >= 0 && index < StudentList.Count - 1)
        {
            SelectedStudent = StudentList[index + 1];
        }
        else
        {
            SelectedStudent = null; 
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevQuestion()
    {
        _currentQuestionIndex--;
        CurrentVisibleQuestion = CurrentStudentQuestions[_currentQuestionIndex];
        UpdateNavigationCommands();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextQuestion()
    {
        _currentQuestionIndex++;
        CurrentVisibleQuestion = CurrentStudentQuestions[_currentQuestionIndex];
        UpdateNavigationCommands();
    }

    private bool CanGoPrev => _currentQuestionIndex > 0;
    private bool CanGoNext => _currentQuestionIndex < CurrentStudentQuestions.Count - 1;

    private void UpdateNavigationCommands()
    {
        PrevQuestionCommand.NotifyCanExecuteChanged();
        NextQuestionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ExportToExcel() // <-- Changed to async Task!
    {
        if (StudentList.Count == 0)
        {
            ErrorMessage = "No graded students to export.";
            return;
        }

        try
        {
            // 1. Generate the suggested file name
            string suggestedName = $"Class_Roster_Export_{DateTime.Now:yyyyMMdd_HHmm}.csv";

            // 2. Open the OS Folder Explorer and wait for the user to pick a spot
            string? customPath = await _fileService.SaveCsvFileAsync(suggestedName);

            // 3. If the user clicks "Cancel" or "X" on the folder popup, just stop safely.
            if (string.IsNullOrEmpty(customPath)) return;

            // 4. Save the file exactly where they asked!
            using (var writer = new StreamWriter(customPath))
            {
                writer.WriteLine("First Name,Middle Name,Last Name,Student ID,Final Score,Needs Review");

                foreach (var student in StudentList)
                {
                    writer.WriteLine($"{student.FirstName},{student.MiddleName},{student.LastName},{student.StudentID},=\"{student.DisplayScore}\",{student.RequiresManualReview}");
                }
            }

            ErrorMessage = $"Successfully exported to: {Path.GetFileName(customPath)}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export failed: {ex.Message}";
        }
    }

}