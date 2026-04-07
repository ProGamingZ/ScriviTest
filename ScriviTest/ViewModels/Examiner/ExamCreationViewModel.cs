using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScriviTest.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace ScriviTest.ViewModels.Examiner;

public partial class ExamCreationViewModel : ViewModelBase
{
    // --- EXAM METADATA BINDINGS ---
    [ObservableProperty] private string _examTitle = string.Empty;
    [ObservableProperty] private string _examDescription = string.Empty;
    [ObservableProperty] private string _teacherName = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _targetSection = string.Empty;
    [ObservableProperty] private int _timeLimitMinutes = 60;
    private readonly Action<ViewModelBase> _navigateAction;
    private readonly Services.FileManagementService _fileService;

    // We changed this from a list of Questions to a list of Sections!
    public ObservableCollection<TestSection> Sections { get; } = new();
    [ObservableProperty]
    private int _totalExamPoints = 0;

    private readonly Services.CryptographyService _cryptoService;

    [ObservableProperty]
    private string _generatedExamKey = string.Empty;

    [ObservableProperty]
    private bool _hasExported = false;
    
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
        // For testing purposes, we will just export it to the desktop
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string xamnPath = Path.Combine(desktopPath, "Draft_Exam.xamn");
        string xamkPath = Path.Combine(desktopPath, "Draft_Exam.xamk");

        var examToExport = new Exam
        {
            Title = string.IsNullOrWhiteSpace(ExamTitle) ? "Untitled Exam" : ExamTitle,
            Instructions = ExamDescription,
            Teacher = TeacherName,
            Subject = Subject,
            Section = TargetSection,
            TimeLimitMinutes = this.TimeLimitMinutes
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
        HasExported = true;

        // TODO: Trigger a UI popup saying "Export Successful!"
        Console.WriteLine($"Successfully generated .xamn and .xamk on the Desktop!");
    }

}