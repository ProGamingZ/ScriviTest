using Avalonia.Controls;
using ScriviTest.ViewModels.Examinee; // Ensure this matches your namespace

namespace ScriviTest.Views.Examinee;

public partial class ExamineeHubView : UserControl
{
    public ExamineeHubView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ExamineeHubViewModel vm)
        {
            // Listen for the "Browse" button click
            vm.OpenFolderPickerRequested += async (sender, args) =>
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                // Open the OS Folder Picker
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select Exam Save Location",
                    AllowMultiple = false
                });

                // If the student selected a folder, pass the path back to the ViewModel
                if (folders.Count >= 1)
                {
                    vm.UpdateSaveLocation(folders[0].Path.LocalPath);
                }
            };
        }
    }
}