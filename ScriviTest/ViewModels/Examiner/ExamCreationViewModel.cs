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
    [ObservableProperty] private bool _hasExported = false;

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
        Services.AppPaths.InitializeFolders();

        string safeTitle = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled_Exam" : ExamTitle.Replace(" ", "_");
        string xamnPath = Path.Combine(Services.AppPaths.QuestionnairesDir, $"Ask_{safeTitle}.xamn");
        string xamkPath = Path.Combine(Services.AppPaths.AnswersDir, $"Ans_{safeTitle}.xamk");
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
        
        // Copy our sections into it
        foreach (var s in Sections)
        {
            examToExport.Sections.Add(s);
        }

        var exportService = new Services.ExportService();
        exportService.ExportExamPackage(examToExport, xamnPath, xamkPath);

        GeneratedExamKey = _cryptoService.GenerateExaminationKey();
        _cryptoService.EncryptFile(xamnPath, GeneratedExamKey);
        exportService.SaveToHistoryLog(examToExport.Title, GeneratedExamKey, xamnPath);
        HasExported = true;

        Console.WriteLine($"Successfully saved exam to: {xamnPath}");
        Console.WriteLine($"History Log Updated!");
    }

}