using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ScriviTest.Services;
using Avalonia.Controls;
namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    #region Dependencies and Constructor
        private readonly Action<ViewModelBase> _navigateAction;
        private readonly Services.FileManagementService _fileService;
        private readonly Services.CryptographyService _cryptoService;

        public ExamCreationViewModel(Action<ViewModelBase> navigateAction)
        {
            _navigateAction = navigateAction;
            _fileService = new Services.FileManagementService();
            _cryptoService = new Services.CryptographyService();

            Sections.CollectionChanged += Sections_CollectionChanged;
            var defaultSection = new TestSection { Title = "Test A: Multiple Choice" };
            defaultSection.Questions.Add(new Question());
            Sections.Add(defaultSection);
        }
    #endregion

    #region Exam Metadata
        [ObservableProperty] private string _examTitle = string.Empty;
        [ObservableProperty] private string _examDescription = string.Empty;
        [ObservableProperty] private string _teacherName = string.Empty;
        [ObservableProperty] private string _subject = string.Empty;
        [ObservableProperty] private string _targetSection = string.Empty;
        [ObservableProperty] private int? _timeLimitMinutes = 60;
        [ObservableProperty] private string _antiCheatStrictness = "Strict";
        [ObservableProperty] private int _totalExamPoints = 0;
    #endregion

    #region Exam Content & Point Calculation
        public ObservableCollection<TestSection> Sections { get; } = new();
        private void Sections_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TestSection s in e.NewItems)
                    s.PropertyChanged += Section_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (TestSection s in e.OldItems)
                    s.PropertyChanged -= Section_PropertyChanged;
            }
            RecalculateGlobalPoints();
        }
        private void Section_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TestSection.SectionTotalPoints))
            {
                RecalculateGlobalPoints();
            }
        }
        private void RecalculateGlobalPoints()
        {
            TotalExamPoints = Sections.Sum(s => s.SectionTotalPoints);
        }
        
        [RelayCommand]
        private void AddSection()
        {
            Sections.Add(new TestSection { Title = $"Test Section {Sections.Count + 1}" });
        }
        
        [RelayCommand]
        private void RemoveSection(TestSection targetSection)
        {
            if (targetSection != null && Sections.Count > 1) // Prevent deleting the very last section
            {
                Sections.Remove(targetSection);
            }
        }
        
        [RelayCommand]
        private void AddMultipleChoice(TestSection targetSection) => targetSection?.Questions.Add(new Question { Type = QuestionType.MultipleChoice });
        
        [RelayCommand]
        private void AddTrueFalse(TestSection targetSection) => targetSection?.Questions.Add(new Question { Type = QuestionType.TrueFalse });
        
        [RelayCommand]
        private void AddMultipleAnswer(TestSection targetSection) => targetSection?.Questions.Add(new Question { Type = QuestionType.MultipleAnswer });
        
        [RelayCommand]
        private void AddEssay(TestSection targetSection) => targetSection?.Questions.Add(new Question { Type = QuestionType.Essay });
        
        [RelayCommand]
        private void RemoveQuestion(Question targetQuestion)
    {
        if (targetQuestion == null) return;

        // Search through all sections to find which one owns this question, then delete it
        foreach (var section in Sections)
        {
            if (section.Questions.Contains(targetQuestion))
            {
                section.Questions.Remove(targetQuestion);
                break;
            }
        }
    }
    #endregion

    #region Media & Image Management
        [RelayCommand]
        private async Task AttachImage(Question targetQuestion)
        {
            if (targetQuestion == null) return;
            var selectedFilePath = await _fileService.PickImageAsync();
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                targetQuestion.AttachedImageFullPath = selectedFilePath;
                targetQuestion.AttachedImageFileName = Path.GetFileName(selectedFilePath);
            }
        }
        
        [RelayCommand]
        private void RemoveImage(Question targetQuestion)
        {
            if (targetQuestion == null) return;
            targetQuestion.AttachedImageFullPath = null;
            targetQuestion.AttachedImageFileName = null;
        }
        
        [RelayCommand]
        private async Task AttachChoiceImage(Choice targetChoice)
        {
            if (targetChoice == null) return;
            var selectedFilePath = await _fileService.PickImageAsync();
            if (!string.IsNullOrEmpty(selectedFilePath))
            {
                targetChoice.AttachedImageFullPath = selectedFilePath;
                targetChoice.AttachedImageFileName = Path.GetFileName(selectedFilePath);
            }
        }
        
        [RelayCommand]
        private void RemoveChoiceImage(Choice targetChoice)
    {
        if (targetChoice == null) return;
        targetChoice.AttachedImageFullPath = null;
        targetChoice.AttachedImageFileName = null;
    }
    
    #endregion

    #region Export & Workspace State
        [ObservableProperty] private bool _isImportedExam = false;
        private string _activeXamnPath = string.Empty;
        private string _activeXamkPath = string.Empty;
        private string _activeDecryptionKey = string.Empty;
        [ObservableProperty] private string _generatedExamKey = string.Empty;

        [RelayCommand]
        private void ExportExam(){
            // 1. Block imported exams
            if (IsImportedExam)
            {
                ShowToast("Export is only for new exams. Click 'Overwrite' instead.", "IconWarning", "DangerBrush"); 
                return;
            }

            Services.AppPaths.InitializeFolders();

            // 2. Calculate intended file names based on the title
            string safeTitle = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled_Exam" : ExamTitle.Replace(" ", "_");
            string xamnPath = Path.Combine(Services.AppPaths.QuestionnairesDir, $"Ask_{safeTitle}.xamn");
            string xamkPath = Path.Combine(Services.AppPaths.AnswersDir, $"Ans_{safeTitle}.xamk");

            // 3. NEW GUARDRAIL: Block the export if the file name already exists!
            if (File.Exists(xamnPath) || File.Exists(xamkPath))
            {
                ShowToast($"An exam named '{safeTitle}' already exists! Please change the Exam Title.", "IconWarning", "DangerBrush");
                return;
            }

            // --- If it passes all checks, proceed with Export ---
            var examToExport = new Exam
            {
                Title = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled Exam" : ExamTitle,
                Instructions = ExamDescription,
                Teacher = TeacherName,
                Subject = Subject,
                Section = TargetSection,
                TimeLimitMinutes = Math.Clamp(TimeLimitMinutes ?? 60, 1, 1440),
                AntiCheatStrictness = AntiCheatStrictness
            };
            
            foreach (var s in Sections)
            {
                examToExport.Sections.Add(s);
            }

            var exportService = new Services.ExportService();
            exportService.ExportExamPackage(examToExport, xamnPath, xamkPath);

            GeneratedExamKey = _cryptoService.GenerateExaminationKey();
            _cryptoService.EncryptFile(xamnPath, GeneratedExamKey);
            
            exportService.SaveToHistoryLog(examToExport.Title, GeneratedExamKey, xamnPath);

            ShowToast($"SUCCESS! Exam Key: {GeneratedExamKey}", "IconKey", "SuccessBrush"); 
        }
        
        [RelayCommand]
        private void OverwriteExam(){
        if (!IsImportedExam)
        {
            ShowToast("Overwrite is only for imported exams. Click 'Export' instead.", "IconWarning", "DangerBrush"); 
            return;
        }

        try
        {
            Services.AppPaths.InitializeFolders();

            var examToExport = new Exam
            {
                Title = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled Exam" : ExamTitle,
                Instructions = ExamDescription,
                Teacher = TeacherName,
                Subject = Subject,
                Section = TargetSection,
                TimeLimitMinutes = Math.Clamp(TimeLimitMinutes ?? 60, 1, 1440),
                AntiCheatStrictness = AntiCheatStrictness
            };
            
            foreach (var s in Sections) examToExport.Sections.Add(s);

            // 1. Calculate the intended NEW paths based on the current title on the screen
            string safeTitle = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled_Exam" : ExamTitle.Replace(" ", "_");
            string newXamnPath = Path.Combine(Services.AppPaths.QuestionnairesDir, $"Ask_{safeTitle}.xamn");
            string newXamkPath = Path.Combine(Services.AppPaths.AnswersDir, $"Ans_{safeTitle}.xamk");

            var exportService = new Services.ExportService();
            
            // 2. Export using the NEW paths
            exportService.ExportExamPackage(examToExport, newXamnPath, newXamkPath);
            _cryptoService.EncryptFile(newXamnPath, _activeDecryptionKey);

            // 3. Update the history log (passing both old and new paths so it can find and update the row)
            exportService.UpdateHistoryLog(examToExport.Title, _activeDecryptionKey, _activeXamnPath, newXamnPath);

            // 4. CLEANUP: If the title changed, delete the old leftover files!
            if (!string.Equals(_activeXamnPath, newXamnPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(_activeXamnPath)) File.Delete(_activeXamnPath);
                if (File.Exists(_activeXamkPath)) File.Delete(_activeXamkPath);
            }

            // 5. Update the active workspace to lock in the new names for the next time they hit Overwrite!
            _activeXamnPath = newXamnPath;
            _activeXamkPath = newXamkPath;

            ShowToast("Successfully overwritten and saved!", "IconSave", "PrimaryBrush"); 
        }
        catch (Exception ex)
        {
            ShowToast($"Overwrite Failed: {ex.Message}", "IconWarning", "DangerBrush");
        }
    }
    
    #endregion

    #region Import Engine & Popup UI
        [ObservableProperty] private bool _isImportPopupVisible = false;
        [ObservableProperty] private string _importXamnPath = string.Empty;
        [ObservableProperty] private string _importXamkPath = string.Empty;
        [ObservableProperty] private string _importDecryptionKey = string.Empty;
        [ObservableProperty] private string _importErrorMessage = string.Empty;

        [RelayCommand]
        private void OpenImportPopup()
        {
            // Clear out any old text when they click the button
            ImportXamnPath = string.Empty;
            ImportXamkPath = string.Empty;
            ImportDecryptionKey = string.Empty;
            ImportErrorMessage = string.Empty;
            IsImportPopupVisible = true;
        }

        [RelayCommand]
        private void CloseImportPopup() => IsImportPopupVisible = false;

        [RelayCommand]
        private async Task BrowseImportXamn()
        {
            // We can reuse the exact same file picker you already built for the Examinee Hub!
            var path = await _fileService.PickExamArchiveAsync(); 
            if (!string.IsNullOrEmpty(path)) ImportXamnPath = path;
        }

        [RelayCommand]
        private async Task BrowseImportXamk()
        {
            // We reuse the Answer Key picker you built!
            var path = await _fileService.PickAnswerKeyAsync(); 
            if (!string.IsNullOrEmpty(path)) ImportXamkPath = path;
        }

        [RelayCommand]
        private void ExecuteImport()
        {
            if (string.IsNullOrWhiteSpace(ImportXamnPath) || string.IsNullOrWhiteSpace(ImportXamkPath))
            {
                ImportErrorMessage = "Please select both the .xamn and .xamk files.";
                return;
            }
            if (string.IsNullOrWhiteSpace(ImportDecryptionKey) || ImportDecryptionKey.Length < 6)
            {
                ImportErrorMessage = "Please enter the full 6-character decryption key.";
                return;
            }

            ImportErrorMessage = "Decrypting and loading...";

            try
            {
                ProcessImportLogic(); 
                IsImportPopupVisible = false;
            }
            catch (Exception ex)
            {
                ImportErrorMessage = $"Import Failed: {ex.Message}. Check your decryption key and files.";
            }
        }

        private void ProcessImportLogic()
        {
        // 1. Setup temporary image extraction folder
        string sessionGuid = Guid.NewGuid().ToString();
        string tempImageFolder = Path.Combine(Path.GetTempPath(), "ScriviTest_Import", sessionGuid);

        // 2. Extract both files
        var studentExam = ExtractStudentExam(tempImageFolder);
        var answerKey = ExtractAnswerKey();

        // 3. Validate they belong together
        ValidateMatchingFiles(studentExam, answerKey);

        // 4. Rebuild the UI
        LoadExamMetadata(studentExam);
        ReconstructExamWorkspace(studentExam, answerKey, tempImageFolder);

        // 5. Lock in the State
        IsImportedExam = true;
        _activeXamnPath = ImportXamnPath;
        _activeXamkPath = ImportXamkPath;
        _activeDecryptionKey = ImportDecryptionKey.ToUpper();
        GeneratedExamKey = _activeDecryptionKey;

        ShowToast("Exam loaded successfully! You may now edit and overwrite.", "📥", "#1976D2");
        }

            private DTOs.StudentExamDto ExtractStudentExam(string tempFolder)
            {
                var studentExam = _cryptoService.DecryptAndExtractExam(ImportXamnPath, ImportDecryptionKey.ToUpper(), tempFolder);
                if (studentExam == null)
                {
                    throw new Exception("Invalid decryption key or corrupted .xamn file.");
                }
                return studentExam;
            }

            private DTOs.AnswerKeyExamDto ExtractAnswerKey()
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(ImportXamkPath);
                var jsonEntry = archive.GetEntry("answer_key.json");
                
                if (jsonEntry == null) 
                    throw new Exception("The .xamk file is corrupted or missing its data.");

                using var reader = new System.IO.StreamReader(jsonEntry.Open());
                string json = reader.ReadToEnd();
                
                var answerKey = System.Text.Json.JsonSerializer.Deserialize<DTOs.AnswerKeyExamDto>(json);
                if (answerKey == null) 
                    throw new Exception("Failed to read the Answer Key data.");

                return answerKey;
            }

            private void ValidateMatchingFiles(DTOs.StudentExamDto studentExam, DTOs.AnswerKeyExamDto answerKey)
            {
                if (studentExam.Sections.Count != answerKey.Sections.Count)
                {
                    throw new Exception("Mismatch Error: The Answer Key does not match this Exam File.");
                }
            }

            private void LoadExamMetadata(DTOs.StudentExamDto studentExam)
            {
                ExamTitle = studentExam.Title;
                ExamDescription = studentExam.Instructions;
                TeacherName = studentExam.Teacher;
                Subject = studentExam.Subject;
                TargetSection = studentExam.Section;
                TimeLimitMinutes = studentExam.TimeLimitMinutes;
                AntiCheatStrictness = studentExam.AntiCheatStrictness;
            }

            private void ReconstructExamWorkspace(DTOs.StudentExamDto studentExam, DTOs.AnswerKeyExamDto answerKey, string tempImageFolder)
            {
                Sections.Clear();

                for (int s = 0; s < studentExam.Sections.Count; s++)
                {
                    var stuSection = studentExam.Sections[s];
                    var keySection = answerKey.Sections[s];

                    var newSection = new TestSection
                    {
                        Title = stuSection.Title,
                        ShuffleQuestions = stuSection.ShuffleQuestions
                    };

                    for (int q = 0; q < stuSection.Questions.Count; q++)
                    {
                        var stuQ = stuSection.Questions[q];
                        var keyQ = keySection.Questions[q];

                        var newQuestion = new Question
                        {
                            Prompt = stuQ.Prompt,
                            Points = keyQ.Points,
                            Type = Enum.Parse<QuestionType>(stuQ.Type),
                            MaxWordCount = stuQ.MaxWordCount,
                            AttachedImageFileName = stuQ.AttachedImageFileName
                        };

                        // Link attached question images
                        if (!string.IsNullOrEmpty(newQuestion.AttachedImageFileName))
                        {
                            newQuestion.AttachedImageFullPath = Path.Combine(tempImageFolder, newQuestion.AttachedImageFileName);
                        }

                        if (newQuestion.Type == QuestionType.MultipleAnswer && Enum.TryParse<ScoringRubric>(keyQ.MultipleAnswerRubric, out var parsedRubric))
                        {
                            newQuestion.MultipleAnswerRubric = parsedRubric;
                        }

                        newQuestion.Choices.Clear();

                        // Reconstruct True/False Data
                        if (newQuestion.Type == QuestionType.TrueFalse)
                        {
                            if (keyQ.CorrectChoiceIndices.Count > 0)
                            {
                                newQuestion.IsTrueFalseAnswerTrue = (keyQ.CorrectChoiceIndices[0] == 0);
                            }
                        }
                        // Reconstruct Multiple Choice / Multiple Answer Data
                        else if (newQuestion.Type != QuestionType.Essay)
                        {
                            for (int c = 0; c < stuQ.Choices.Count; c++)
                            {
                                var stuChoice = stuQ.Choices[c];
                                var newChoice = new Choice
                                {
                                    Text = stuChoice.Text,
                                    AttachedImageFileName = stuChoice.AttachedImageFileName,
                                    IsCorrect = keyQ.CorrectChoiceIndices.Contains(c)
                                };

                                // Link attached choice images
                                if (!string.IsNullOrEmpty(newChoice.AttachedImageFileName))
                                {
                                    newChoice.AttachedImageFullPath = Path.Combine(tempImageFolder, newChoice.AttachedImageFileName);
                                }

                                newQuestion.Choices.Add(newChoice);
                            }
                        }

                        newSection.Questions.Add(newQuestion);
                    }

                    Sections.Add(newSection);
                }
            }


    #endregion

    #region Toast Notification System
        private IBrush? GetBrush(string resourceKey)
        {
            var app = Application.Current;
            if (app != null && app.TryGetResource(resourceKey, app.ActualThemeVariant, out var res) && res is IBrush brush)
                return brush;
            return Brushes.Gray; // Fallback
        }

        private string GetIcon(string resourceKey, string fallbackIcon)
        {
            if (Application.Current != null && Application.Current.TryGetResource(resourceKey, out var res) && res is string iconStr)
                return iconStr;
            return fallbackIcon; 
        }
        private int _currentToastId = 0;
        [ObservableProperty] private bool _isNotificationVisible = false;
        [ObservableProperty] private string _notificationMessage = string.Empty;
        [ObservableProperty] private string _notificationIcon = string.Empty;
        [ObservableProperty] private IBrush? _notificationColor;
        
        [RelayCommand] private void CloseToast() => IsNotificationVisible = false;
        private async void ShowToast(string message, string iconKey, string colorKey)
        {
            NotificationMessage = message;
            NotificationIcon = GetIcon(iconKey, "ℹ️");
            NotificationColor = GetBrush(colorKey);
            IsNotificationVisible = true;

            int thisToastId = ++_currentToastId;

            await Task.Delay(5000);

            if (_currentToastId == thisToastId)
            {
                IsNotificationVisible = false;
            }
        }

    #endregion

    #region Navigation
        [RelayCommand]
        private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));
    #endregion

}