using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace ScriviTest.ViewModels.Examiner;

public partial class GradingHubViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;
    private readonly Services.CryptographyService _cryptoService;

    [ObservableProperty]
    private string? _answerKeyPath;

    [ObservableProperty]
    private string _answerKeyFileName = "No answer key selected.";

    [ObservableProperty]
    private List<string> _studentSubmissionPaths = new();

    [ObservableProperty]
    private string _studentFilesSummary = "0 student submissions loaded.";

    [ObservableProperty]
    private string _whiteboardKey = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public GradingHubViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService();
        _cryptoService = new Services.CryptographyService();
    }

    // Assuming the user navigates here from the Examiner Hub
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
            StudentFilesSummary = $"{paths.Count} student submission(s) loaded for grading.";
        }
    }

    private bool CanStartGrading => !string.IsNullOrEmpty(AnswerKeyPath) && StudentSubmissionPaths.Count > 0 && WhiteboardKey.Length >= 6;

    partial void OnAnswerKeyPathChanged(string? value) => StartGradingCommand.NotifyCanExecuteChanged();
    partial void OnStudentSubmissionPathsChanged(List<string> value) => StartGradingCommand.NotifyCanExecuteChanged();
    partial void OnWhiteboardKeyChanged(string value) => StartGradingCommand.NotifyCanExecuteChanged();

    
    [RelayCommand(CanExecute = nameof(CanStartGrading))]
    private void StartGrading()
    {
        ErrorMessage = "Running Auto-Grader...";

        // 1. READ THE ANSWER KEY (.xamk)
        DTOs.AnswerKeyExamDto? answerKey;
        try
        {
            string keyJson = File.ReadAllText(AnswerKeyPath!);
            answerKey = JsonSerializer.Deserialize<DTOs.AnswerKeyExamDto>(keyJson);
            if (answerKey == null) throw new Exception("Answer Key is empty.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to read Answer Key: {ex.Message}";
            return;
        }

        // 2. BATCH DECRYPT STUDENT SUBMISSIONS
        var decryptedSubmissions = new List<DTOs.StudentSubmissionDto>();
        foreach (var path in StudentSubmissionPaths)
        {
            var submission = _cryptoService.DecryptStudentSubmission(path, WhiteboardKey.ToUpper());
            if (submission != null) decryptedSubmissions.Add(submission);
            else
            {
                ErrorMessage = $"Access Denied: Failed to decrypt {Path.GetFileName(path)}.";
                return;
            }
        }

        // 3. THE AUTO-GRADER ENGINE
        var finalGrades = new List<Models.GradeReport>();

        foreach (var student in decryptedSubmissions)
        {
            var report = new Models.GradeReport 
            { 
                StudentName = student.StudentName,
                MaxPossiblePoints = 0,
                TotalPointsEarned = 0,
                RequiresManualReview = false
            };

            // Loop through Sections
            for (int s = 0; s < student.Sections.Count; s++)
            {
                if (s >= answerKey.Sections.Count) continue; // Safety check
                
                var studentSection = student.Sections[s];
                var keySection = answerKey.Sections[s];

                // Loop through Questions
                for (int q = 0; q < studentSection.Questions.Count; q++)
                {
                    if (q >= keySection.Questions.Count) continue; // Safety check

                    var studentQ = studentSection.Questions[q];
                    var keyQ = keySection.Questions[q];

                    report.MaxPossiblePoints += keyQ.Points;

                    // GRADE: ESSAY
                    if (keyQ.Type == "Essay")
                    {
                        report.RequiresManualReview = true;
                        // Essays get 0 auto-points. Teacher must grade manually later.
                        continue; 
                    }

                    // GRADE: MULTIPLE CHOICE or TRUE/FALSE
                    if (keyQ.Type == "MultipleChoice" || keyQ.Type == "TrueFalse")
                    {
                        // Check if the exact single correct index was selected
                        if (studentQ.SelectedChoiceIndices.Count == 1 && 
                            keyQ.CorrectChoiceIndices.Contains(studentQ.SelectedChoiceIndices[0]))
                        {
                            report.TotalPointsEarned += keyQ.Points;
                        }
                    }

                    // GRADE: MULTIPLE ANSWER
                    if (keyQ.Type == "MultipleAnswer")
                    {
                        int correctGuesses = 0;
                        int wrongGuesses = 0;

                        foreach (var selectedIndex in studentQ.SelectedChoiceIndices)
                        {
                            if (keyQ.CorrectChoiceIndices.Contains(selectedIndex)) correctGuesses++;
                            else wrongGuesses++;
                        }

                        if (keyQ.MultipleAnswerRubric == "All-or-Nothing")
                        {
                            // Must have exact same number of correct choices, and zero wrong choices
                            if (wrongGuesses == 0 && correctGuesses == keyQ.CorrectChoiceIndices.Count)
                            {
                                report.TotalPointsEarned += keyQ.Points;
                            }
                        }
                        else // Partial Credit (-1 Penalty)
                        {
                            // Calculate percentage of correct answers found, minus a penalty for guessing wrong things
                            double pointPerCorrect = (double)keyQ.Points / keyQ.CorrectChoiceIndices.Count;
                            double pointsEarned = (correctGuesses * pointPerCorrect) - (wrongGuesses * pointPerCorrect);
                            
                            // Prevent negative scores on a question
                            if (pointsEarned > 0) report.TotalPointsEarned += pointsEarned;
                        }
                    }
                }
            }
            finalGrades.Add(report);
        }

        // 4. EXPORT THE CSV GRADE REPORT
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        
        // Generate a filename with a timestamp so teachers don't accidentally overwrite older reports
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string csvPath = Path.Combine(desktopPath, $"ScriviTest_Grades_{timestamp}.csv");

        var exportService = new Services.ExportService();
        exportService.ExportGradeReportToCsv(finalGrades, csvPath);

        // 5. Update the UI to show total success!
        ErrorMessage = $"SUCCESS! Graded {finalGrades.Count} exams. CSV Report saved to your Desktop.";
    }
}