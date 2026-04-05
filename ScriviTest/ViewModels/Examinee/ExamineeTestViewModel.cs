using CommunityToolkit.Mvvm.ComponentModel;
using ScriviTest.DTOs;
using System;

namespace ScriviTest.ViewModels;

public partial class ExamineeTestViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateAction;
    
    // This holds the actual test data we decrypted!
    [ObservableProperty]
    private StudentExamDto _examData;

    [ObservableProperty]
    private string _imageDirectory;

    public ExamineeTestViewModel(Action<ViewModelBase> navigateAction, StudentExamDto decryptedExam, string tempDirectory)
    {
        _navigateAction = navigateAction;
        ExamData = decryptedExam;
        ImageDirectory = tempDirectory;
    }
}