using Avalonia.Controls;
using ScriviTest.ViewModels.Examiner;
using System.Linq;

namespace ScriviTest.Views.Examiner;

public partial class ExamHistoryView : UserControl
{
    public ExamHistoryView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ExamHistoryViewModel vm)
        {
            // Listen for the "Relink" button click
            vm.OpenFilePickerRequested += async (sender, wrapper) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Locate Missing Exam File",
                    AllowMultiple = false
                });

                if (files.Count >= 1)
                {
                    // Pass the new file path back to the ViewModel to save
                    vm.UpdateFilePath(wrapper, files[0].Path.LocalPath);
                }
            };
        }
    }
}