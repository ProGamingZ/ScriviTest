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
     
    #region Dependencies & Constructors
        private readonly Action<ViewModelBase> _navigateAction;
        private readonly Services.FileManagementService _fileService;
        private readonly Services.CryptographyService _cryptoService;
        private readonly string _examinerTempImageDir = Path.Combine(Path.GetTempPath(), "ScriviTest", "ExaminerKeyMedia");
        private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> _imageCache = new();

        public GradingHubViewModel(Action<ViewModelBase> navigateAction)
        {
            _navigateAction = navigateAction;
            _fileService = new Services.FileManagementService();
            _cryptoService = new Services.CryptographyService();
        }
    #endregion

    #region UI Toggles & Global State
        [ObservableProperty] private bool _showFirstName = true;
        [ObservableProperty] private bool _showMiddleName = true;
        [ObservableProperty] private bool _showLastName = true;
        [ObservableProperty] private bool _showID = true;
        [ObservableProperty] private bool _showScores = true;
    #endregion

    #region Toast Notification System
        private int _currentToastId;
        [ObservableProperty] private bool _isNotificationVisible;
        [ObservableProperty] private string _notificationMessage = string.Empty;
        [ObservableProperty] private string _notificationIcon = "ℹ️";
        [ObservableProperty] private string _notificationColor = "#323232";

        public async void ShowToast(string message, string icon = "ℹ️", string hexColor = "#323232")
        {
            NotificationMessage = message;
            NotificationIcon = icon;
            NotificationColor = hexColor;
            IsNotificationVisible = true;

            // Auto-hide after 3.5 seconds
            int toastId = ++_currentToastId;
            await Task.Delay(3500);
            if (_currentToastId == toastId) IsNotificationVisible = false;
        }

        [RelayCommand]
        private void CloseToast() => IsNotificationVisible = false;
    #endregion

    #region File Ingestion & Decryption
        private DTOs.AnswerKeyExamDto? _loadedAnswerKey;
        [ObservableProperty] private string? _answerKeyPath;
        [ObservableProperty] private string _answerKeyFileName = string.Empty;
        [ObservableProperty] private List<string> _studentSubmissionPaths = new();
        [ObservableProperty] private string _studentFilesSummary = string.Empty;
        [ObservableProperty] private string _examKey = string.Empty;

        // Validation for the Check button
        private bool CanCheckAndUnlock => !string.IsNullOrEmpty(AnswerKeyPath) && StudentSubmissionPaths.Count > 0 && ExamKey.Length >= 6;
        partial void OnAnswerKeyPathChanged(string? value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();
        partial void OnStudentSubmissionPathsChanged(List<string> value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();
        partial void OnExamKeyChanged(string value) => CheckAndUnlockCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private async Task BrowseForAnswerKey()
        {
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
            var paths = await _fileService.PickStudentSubmissionsAsync();
            if (paths.Count > 0)
            {
                StudentSubmissionPaths = paths;
                StudentFilesSummary = $"{paths.Count} student submission(s) loaded.";
            }
        }
        
        [RelayCommand(CanExecute = nameof(CanCheckAndUnlock))]
        private void CheckAndUnlock()
        {
            StudentList.Clear(); // Clear old list if they click it twice

            try
            {
                // 1. Prepare the Answer Key and Images
                _loadedAnswerKey = LoadAndCacheAnswerKey();
                // 2. Decrypt, Grade, and load the students into the UI
                ProcessStudentSubmissions(_loadedAnswerKey);
            }
            catch (Exception ex)
            {
                ShowToast($"Unlocking Failed: {ex.Message}", "🛑", "#D32F2F");
            }
        }

        private DTOs.AnswerKeyExamDto LoadAndCacheAnswerKey()
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
                    
                    using (var stream = File.OpenRead(destPath))
                    {
                        // Crush the reference images down to 600px wide
                        _imageCache[entry.Name] = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, 600);
                    }
                }
            }

            var jsonEntry = archive.GetEntry("answer_key.json");
            if (jsonEntry == null) throw new Exception("Invalid Answer Key Format.");
            
            using var reader = new StreamReader(jsonEntry.Open());
            var key = JsonSerializer.Deserialize<DTOs.AnswerKeyExamDto>(reader.ReadToEnd());
            
            if (key == null) throw new Exception("Failed to read Answer Key data.");
            
            return key;
        }
        private void ProcessStudentSubmissions(DTOs.AnswerKeyExamDto answerKey)
        {
            foreach (var path in StudentSubmissionPaths)
            {
                var studentSubmission = _cryptoService.DecryptStudentSubmission(path, ExamKey.ToUpper());
                if (studentSubmission == null)
                {
                    throw new Exception($"Access Denied: Failed to decrypt {Path.GetFileName(path)}. Wrong Exam Key?");
                }

                // Grade the individual paper and add it to the UI Roster
                var report = GradeStudentSubmission(studentSubmission, answerKey, path);
                StudentList.Add(report);
            }
        }
        private Models.GradeReport GradeStudentSubmission(DTOs.StudentSubmissionDto studentSubmission, DTOs.AnswerKeyExamDto answerKey, string filePath)
        {
            var report = new Models.GradeReport 
            { 
                FirstName = studentSubmission.FirstName,
                MiddleName = studentSubmission.MiddleName,
                LastName = studentSubmission.LastName,
                StudentID = studentSubmission.StudentID,
                SubmissionData = studentSubmission, // keep raw data Middle Panel
                FilePath = filePath,
                MaxPossiblePoints = 0,
                TotalPointsEarned = 0,
                RequiresManualReview = false
            };

            // Calculate autograde & inject directly into DTO
            for (int s = 0; s < studentSubmission.Sections.Count; s++)
            {
                if (s >= answerKey.Sections.Count) continue;
                for (int q = 0; q < studentSubmission.Sections[s].Questions.Count; q++)
                {
                    if (q >= answerKey.Sections[s].Questions.Count) continue;
                    var studentQ = studentSubmission.Sections[s].Questions[q];
                    var keyQ = answerKey.Sections[s].Questions[q];

                    report.MaxPossiblePoints += keyQ.Points;

                    // If not graded yet, run Auto-Grader
                    if (!studentQ.AwardedPoints.HasValue)
                    {
                        double autoGrade = 0;
                        if (keyQ.Type == "Essay") 
                        {
                            report.RequiresManualReview = true;
                        }
                        else if (keyQ.Type == "MultipleChoice" || keyQ.Type == "TrueFalse")
                        {
                            if (studentQ.SelectedChoiceIndices.Count == 1 && keyQ.CorrectChoiceIndices.Contains(studentQ.SelectedChoiceIndices[0]))
                                autoGrade = keyQ.Points;
                        }
                        else if (keyQ.Type == "MultipleAnswer")
                        {
                            var studentAns = new HashSet<int>(studentQ.SelectedChoiceIndices);
                            var keyAns = new HashSet<int>(keyQ.CorrectChoiceIndices);
                            if (keyQ.MultipleAnswerRubric == "AllOrNothing") 
                            { 
                                if (studentAns.SetEquals(keyAns)) autoGrade = keyQ.Points; 
                            }
                            else
                            {
                                double pointsPerCorrect = keyAns.Count > 0 ? (double)keyQ.Points / keyAns.Count : 0;
                                foreach (int ans in studentAns) 
                                { 
                                    if (keyAns.Contains(ans)) autoGrade += pointsPerCorrect; 
                                    else autoGrade -= 1; 
                                }
                                if (autoGrade < 0) autoGrade = 0; 
                            }
                        }
                        studentQ.AwardedPoints = autoGrade; // save auto-grade into DTO!
                    }

                    report.TotalPointsEarned += studentQ.AwardedPoints.Value;
                }
            }
            
            return report;
        }

    #endregion

    #region Student Roster Management
        [ObservableProperty] private ObservableCollection<Models.GradeReport> _studentList = new();
        [ObservableProperty] private Models.GradeReport? _selectedStudent;  

        partial void OnSelectedStudentChanged(Models.GradeReport? value)
        {
            // 1. Guard Clause: If they deselect or data is missing, wipe the UI
            if (value == null || value.SubmissionData == null || _loadedAnswerKey == null)
            {
                ClearReviewPanel();
                return;
            }

            // 2. Build the brand-new lists in memory (Using a Tuple to return both lists at once!)
            var (freshFlatList, freshSectionList) = BuildReviewWorkspace(value);

            // 3. Swap the lists to trigger Avalonia's instant visual update
            CurrentStudentQuestions = freshFlatList;
            CurrentReviewSections = freshSectionList;

            // 4. Set up the active question and metadata
            if (CurrentStudentQuestions.Count > 0)
            {
                CurrentVisibleQuestion = CurrentStudentQuestions[0];
                UpdateNavigationCommands();
            }

            UpdateReviewMetadata(value);
            RecalculateCurrentScore();
        }
        private void ClearReviewPanel()
        {
            _currentQuestionIndex = 0;
            CurrentVisibleQuestion = null;
            CurrentStudentQuestions = new ObservableCollection<Models.ReviewQuestion>();
            CurrentReviewSections = new ObservableCollection<Models.ReviewSection>();
            CurrentTimeTakenDisplay = string.Empty;
            CurrentIncidentLog.Clear();
        }
        private (ObservableCollection<Models.ReviewQuestion>, ObservableCollection<Models.ReviewSection>) BuildReviewWorkspace(Models.GradeReport studentReport)
        {
            var flatList = new ObservableCollection<Models.ReviewQuestion>();
            var sectionList = new ObservableCollection<Models.ReviewSection>();
            int qNumber = 1;

            for (int s = 0; s < studentReport.SubmissionData!.Sections.Count; s++)
            {
                if (s >= _loadedAnswerKey!.Sections.Count) continue;
                var studentSection = studentReport.SubmissionData.Sections[s];
                var keySection = _loadedAnswerKey.Sections[s];

                var reviewSection = new Models.ReviewSection
                {
                    Title = string.IsNullOrEmpty(keySection.Title) ? $"Section {s + 1}" : keySection.Title
                };

                for (int q = 0; q < studentSection.Questions.Count; q++)
                {
                    if (q >= keySection.Questions.Count) continue;
                    
                    // We pass 'dynamic' here so we don't have to worry about the exact DTO class names
                    var reviewQ = BuildReviewQuestion(studentSection.Questions[q], keySection.Questions[q], qNumber++);
                    
                    reviewSection.Questions.Add(reviewQ);
                    flatList.Add(reviewQ);
                }
                
                if (reviewSection.Questions.Count > 0) sectionList.Add(reviewSection);
            }

            return (flatList, sectionList);
        }
        private Models.ReviewQuestion BuildReviewQuestion(dynamic studentQ, dynamic keyQ, int qNumber)
        {
            double earnedPoints = studentQ.AwardedPoints ?? 0;
            bool isSingleSelection = keyQ.Type == "MultipleChoice" || keyQ.Type == "TrueFalse";

            var reviewQ = new Models.ReviewQuestion
            {
                QuestionNumber = qNumber,
                Prompt = keyQ.Prompt,
                Type = keyQ.Type,
                MaxPoints = keyQ.Points,
                EssayResponse = studentQ.EssayResponse ?? string.Empty,
                PointsAwarded = earnedPoints,
                Remarks = studentQ.Remarks ?? string.Empty
            };

            // Attach event to live-update the score if the teacher edits points manually
            reviewQ.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(Models.ReviewQuestion.PointsAwarded))
                    RecalculateCurrentScore();
            };

            string qImgName = (string)keyQ.AttachedImageFileName;
        
            // Link Question Image from RAM Cache
            if (!string.IsNullOrEmpty(qImgName) && _imageCache.TryGetValue(qImgName, out var cachedQImg))
            {
                reviewQ.ImageBitmap = cachedQImg;
            }
            
            // Build Choices
            for (int c = 0; c < keyQ.Choices.Count; c++)
            {
                var reviewChoice = new Models.ReviewChoice
                {
                    Text = keyQ.Choices[c].Text,
                    IsCorrectAnswer = keyQ.CorrectChoiceIndices.Contains(c),
                    IsStudentSelected = studentQ.SelectedChoiceIndices.Contains(c),
                    IsSingleSelection = isSingleSelection
                };

                // Link Choice Image from RAM Cache
                string cImgName = (string)keyQ.Choices[c].AttachedImageFileName;
            
                // Link Choice Image from RAM Cache 
                if (!string.IsNullOrEmpty(cImgName) && _imageCache.TryGetValue(cImgName, out var cachedCImg))
                {
                    reviewChoice.ImageBitmap = cachedCImg;
                }

                reviewQ.Choices.Add(reviewChoice);
            }

            return reviewQ;
        }
        private void UpdateReviewMetadata(Models.GradeReport studentReport)
        {
            CurrentMaxScore = studentReport.MaxPossiblePoints;
            CurrentTimeTakenDisplay = studentReport.SubmissionData!.TimeTakenDisplay;

            CurrentIncidentLog.Clear();
            if (studentReport.SubmissionData.IncidentLog != null)
            {
                foreach (var log in studentReport.SubmissionData.IncidentLog)
                {
                    CurrentIncidentLog.Add(log);
                }
            }
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
                        studentQ.Remarks = CurrentStudentQuestions[flatIndex].Remarks;
                        flatIndex++;
                    }
                }
            }
            // 2. OVERWRITE THE .XANS FILE SECURELY ON THE HARD DRIVE
            try
            {
                string jsonContent = JsonSerializer.Serialize(SelectedStudent.SubmissionData, new JsonSerializerOptions { WriteIndented = true });
                // Write the unencrypted JSON temporarily, then encrypt it in-place using the Exam key!
                File.WriteAllText(SelectedStudent.FilePath, jsonContent);
                _cryptoService.EncryptFile(SelectedStudent.FilePath, ExamKey.ToUpper());
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to save grades: {ex.Message}", "🛑", "#D32F2F");
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
                { SelectedStudent = StudentList[index + 1]; }
            else
                { SelectedStudent = null; }
        }

    #endregion

    #region Active Review Panel & Navigation
        private int _currentQuestionIndex = 0;
        [ObservableProperty] private string _currentTimeTakenDisplay = string.Empty;
        [ObservableProperty] private ObservableCollection<Models.ReviewSection> _currentReviewSections = new();
        [ObservableProperty] private ObservableCollection<Models.ReviewQuestion> _currentStudentQuestions = new();
        [ObservableProperty] private Models.ReviewQuestion? _currentVisibleQuestion;
        [ObservableProperty]  private System.Collections.ObjectModel.ObservableCollection<string> _currentIncidentLog = new();
        [ObservableProperty] private double _currentTotalScore = 0;
        [ObservableProperty] private int _currentMaxScore = 0;

        private void RecalculateCurrentScore()
        {
            double total = 0;
            foreach (var q in CurrentStudentQuestions) total += q.PointsAwarded;
            CurrentTotalScore = total;
        }
        private bool CanGoPrev => _currentQuestionIndex > 0;
        private bool CanGoNext => _currentQuestionIndex < CurrentStudentQuestions.Count - 1;
        private void UpdateNavigationCommands()
        {
            PrevQuestionCommand.NotifyCanExecuteChanged();
            NextQuestionCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void JumpToQuestion(Models.ReviewQuestion question)
        {
            if (question == null) return;
            _currentQuestionIndex = CurrentStudentQuestions.IndexOf(question);
            CurrentVisibleQuestion = question;
            UpdateNavigationCommands();
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
    #endregion

    #region Export Engine
        [RelayCommand]
        private async Task ExportReports() 
        {
            if (StudentList.Count == 0)
            {
                ShowToast("No graded students to export.", "IconWarning", "WarningBrush");
                return;
            }

            if (_loadedAnswerKey == null)
            {
                ShowToast("Critical Error: Answer key is missing from memory.", "IconWarning", "DangerBrush");
                return;
            }

            try
            {
                // 1. Ask the user where to save the Master Folder
                string? targetDirectory = await _fileService.PickExportDirectoryAsync();
                
                // If they hit "Cancel" or "X" on the popup, stop safely
                if (string.IsNullOrEmpty(targetDirectory)) return; 

                // 2. Create a dedicated folder for this specific export session
                string sessionFolderName = $"ScriviTest_Export_{DateTime.Now:yyyyMMdd_HHmm}";
                string masterExportPath = Path.Combine(targetDirectory, sessionFolderName);
                Directory.CreateDirectory(masterExportPath);

                // 3. Create the Sub-Folder for the HTML Reports
                string htmlFolderPath = Path.Combine(masterExportPath, "Detailed_Student_Reports");
                Directory.CreateDirectory(htmlFolderPath);

                // 4. Generate the Master CSV File
                string csvPath = Path.Combine(masterExportPath, "Class_Roster_Summary.csv");
                using (var writer = new StreamWriter(csvPath))
                {
                    writer.WriteLine("First Name,Middle Name,Last Name,Student ID,Final Score,Needs Review");

                    foreach (var student in StudentList)
                    {
                        writer.WriteLine($"{student.FirstName},{student.MiddleName},{student.LastName},{student.StudentID},=\"{student.DisplayScore}\",{student.RequiresManualReview}");
                    }
                }

                // 5. Generate the individual HTML files!
                foreach (var student in StudentList)
                {
                    // Create a safe file name (e.g., "Doe_John_Report.html")
                    string safeName = $"{student.LastName}_{student.FirstName}".Replace(" ", "_");
                    // Remove any weird characters that Windows/Mac hate in file names
                    foreach (char c in Path.GetInvalidFileNameChars()) { safeName = safeName.Replace(c.ToString(), ""); }

                    string htmlFilePath = Path.Combine(htmlFolderPath, $"{safeName}_Report.html");

                    // Use the tool we just built!
                    string htmlContent = Services.HtmlReportGenerator.GenerateStudentReport(student, _loadedAnswerKey);
                    
                    File.WriteAllText(htmlFilePath, htmlContent);
                }

                ShowToast($"SUCCESS! Saved to {sessionFolderName}", "✅", "#388E3C");
            }
            catch (Exception ex)
            {
                ShowToast($"Export failed: {ex.Message}", "🛑", "#D32F2F");
            }
        }
    #endregion

    #region App Navigation
        [RelayCommand]
        private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));
    #endregion

}