using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ScriviTest.Services;
namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    // --- EXAM METADATA BINDINGS ---
    [ObservableProperty] private string _examTitle = string.Empty;
    [ObservableProperty] private string _examDescription = string.Empty;
    [ObservableProperty] private string _teacherName = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _targetSection = string.Empty;
    [ObservableProperty] private int? _timeLimitMinutes = 60;
    [ObservableProperty] private string _antiCheatStrictness = "Strict";
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;

    // We changed this from a list of Questions to a list of Sections!
    public ObservableCollection<TestSection> Sections { get; } = new();
    [ObservableProperty]
    private int _totalExamPoints = 0;

    private readonly Services.CryptographyService _cryptoService;

    [ObservableProperty] private string _generatedExamKey = string.Empty;

    [ObservableProperty] private bool _isImportPopupVisible = false;
    [ObservableProperty] private string _importXamnPath = string.Empty;
    [ObservableProperty] private string _importXamkPath = string.Empty;
    [ObservableProperty] private string _importDecryptionKey = string.Empty;
    [ObservableProperty] private string _importErrorMessage = string.Empty;
    [ObservableProperty] private bool _isImportedExam = false;
    private string _activeXamnPath = string.Empty;
    private string _activeXamkPath = string.Empty;
    private string _activeDecryptionKey = string.Empty;

    [ObservableProperty] private bool _isNotificationVisible = false;
    [ObservableProperty] private string _notificationMessage = string.Empty;
    [ObservableProperty] private string _notificationIcon = string.Empty;
    [ObservableProperty] private string _notificationColor = "#323232";
    private int _currentToastId = 0;
    [RelayCommand] private void CloseToast() => IsNotificationVisible = false;
    private async void ShowToast(string message, string icon, string colorHex)
    {
        NotificationMessage = message;
        NotificationIcon = icon;
        NotificationColor = colorHex;
        IsNotificationVisible = true;

        int thisToastId = ++_currentToastId;

        await Task.Delay(5000);

        if (_currentToastId == thisToastId)
        {
            IsNotificationVisible = false;
        }
    }

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
        // 1. Validate that they filled out everything
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
            // 2. We will build this massive method in the next step!
            ProcessImportLogic(); 
            
            // 3. If it succeeds without crashing, hide the popup
            IsImportPopupVisible = false;
        }
        catch (Exception ex)
        {
            ImportErrorMessage = $"Import Failed: {ex.Message}. Check your decryption key and files.";
        }
    }

    private void ProcessImportLogic()
    {
        // 1. Decrypt and Extract the Student Exam (.xamn)
        string sessionGuid = Guid.NewGuid().ToString();
        string tempImageFolder = Path.Combine(Path.GetTempPath(), "ScriviTest_Import", sessionGuid);
        
        // This leverages the service you already built to unlock the student file!
        var studentExam = _cryptoService.DecryptAndExtractExam(ImportXamnPath, ImportDecryptionKey.ToUpper(), tempImageFolder);
        
        if (studentExam == null)
        {
            throw new Exception("Invalid decryption key or corrupted .xamn file.");
        }

        // 2. Unzip and read the Answer Key (.xamk)
        DTOs.AnswerKeyExamDto? answerKey = null;
        using (var archive = System.IO.Compression.ZipFile.OpenRead(ImportXamkPath))
        {
            var jsonEntry = archive.GetEntry("answer_key.json");
            if (jsonEntry == null) throw new Exception("The .xamk file is corrupted or missing its data.");

            using (var reader = new System.IO.StreamReader(jsonEntry.Open()))
            {
                string json = reader.ReadToEnd();
                answerKey = System.Text.Json.JsonSerializer.Deserialize<DTOs.AnswerKeyExamDto>(json);
            }
        }

        // Security Check: Ensure the Answer Key actually belongs to this Exam!
        if (answerKey == null || studentExam.Sections.Count != answerKey.Sections.Count)
        {
            throw new Exception("The Answer Key does not match this Exam File.");
        }

        // 3. Populate Exam Metadata (Pulled from the .xamn file)
        ExamTitle = studentExam.Title;
        ExamDescription = studentExam.Instructions;
        TeacherName = studentExam.Teacher;
        Subject = studentExam.Subject;
        TargetSection = studentExam.Section;
        TimeLimitMinutes = studentExam.TimeLimitMinutes;
        AntiCheatStrictness = studentExam.AntiCheatStrictness;

        // 4. Clear the current workspace!
        Sections.Clear();

        // 5. The Stitching Loop: Merge the Student Questions with the Key Answers
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

                // Link attached question images to the extracted Temp folder
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
                            IsCorrect = keyQ.CorrectChoiceIndices.Contains(c) // Marries the answer back to the UI!
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
        IsImportedExam = true;
        _activeXamnPath = ImportXamnPath;
        _activeXamkPath = ImportXamkPath;
        _activeDecryptionKey = ImportDecryptionKey.ToUpper();
        GeneratedExamKey = ImportDecryptionKey.ToUpper(); 
        ShowToast("Exam loaded successfully! You may now edit and overwrite.", "📥", "#1976D2");
    }

    public ExamCreationViewModel(Action<ViewModelBase> navigateAction)
    {
        _navigateAction = navigateAction;
        _fileService = new Services.FileManagementService();
        _cryptoService = new Services.CryptographyService();

        Sections.CollectionChanged += Sections_CollectionChanged;
        // Create a default section with one default question so the screen isn't blank
        var defaultSection = new TestSection { Title = "Test A: Multiple Choice" };
        defaultSection.Questions.Add(new Question());
        Sections.Add(defaultSection);
    }

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
        // If a section's sub-total changes, update the global total
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
    private void GoBack() => _navigateAction(new ExaminerHubViewModel(_navigateAction));

    // --- SECTION COMMANDS ---
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

    // --- QUESTION COMMANDS (Now targeted at specific Sections) ---
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

    // --- IMAGE COMMANDS ---
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

    [RelayCommand]
    private async Task ExportExam()
    {
        // 1. Block imported exams
        if (IsImportedExam)
        {
            ShowToast("Export is only for new exams. Click 'Overwrite' instead.", "⚠️", "#D32F2F"); 
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
            ShowToast($"An exam named '{safeTitle}' already exists! Please change the Exam Title.", "🛑", "#D32F2F");
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

        ShowToast($"SUCCESS! Exam Key: {GeneratedExamKey}", "✅", "#2E7D32"); 
    }

    [RelayCommand]
    private void OverwriteExam()
    {
        if (!IsImportedExam)
        {
            ShowToast("Overwrite is only for imported exams. Click 'Export' instead.", "⚠️", "#D32F2F"); 
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

            ShowToast("Successfully overwritten and saved!", "💾", "#1976D2"); 
        }
        catch (Exception ex)
        {
            ShowToast($"Overwrite Failed: {ex.Message}", "❌", "#D32F2F");
        }
    }

}