using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace ScriviTest.Services;

public class FileManagementService
{
    // Opens the OS-native file picker and returns the full path of the selected image
    public async Task<string?> PickImageAsync()
    {
        // 1. Get the current desktop application window to attach the dialog to
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                // 2. Open the Avalonia 11 Storage Provider
                var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select an Image to Attach",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { FilePickerFileTypes.ImageAll } // Restricts to .jpg, .png, etc.
                });

                // 3. Return the full local path if a file was selected
                if (files.Count >= 1)
                {
                    return files[0].Path.LocalPath;
                }
            }
        }
        
        return null; // User canceled the dialog
    }

    // Opens the file picker specifically looking for ScriviTest .xamn files
    public async Task<string?> PickExamArchiveAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                // Create a custom filter so the file browser specifically looks for your app's files
                var xamnFileType = new FilePickerFileType("ScriviTest Exam Archive")
                {
                    Patterns = new[] { "*.xamn" }
                };

                var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select a ScriviTest Examination File",
                    AllowMultiple = false,
                    FileTypeFilter = new[] { xamnFileType, FilePickerFileTypes.All }
                });

                if (files.Count >= 1)
                {
                    return files[0].Path.LocalPath;
                }
            }
        }
        
        return null; // User canceled the dialog
    }
}