using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
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

    // Looks specifically for the .xamk Answer Key
    public async Task<string?> PickAnswerKeyAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var xamkFileType = new FilePickerFileType("ScriviTest Answer Key") { Patterns = new[] { "*.xamk" } };
            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select the Examination Answer Key (.xamk)",
                AllowMultiple = false,
                FileTypeFilter = new[] { xamkFileType, FilePickerFileTypes.All }
            });

            if (files.Count >= 1) return files[0].Path.LocalPath;
        }
        return null;
    }

    // Looks for .xans files and allows the user to highlight dozens of them at once!
    public async Task<List<string> > PickStudentSubmissionsAsync()
    {
        var selectedPaths = new List<string>();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var xansFileType = new FilePickerFileType("ScriviTest Student Submission") { Patterns = new[] { "*.xans" } };
            var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Student Submissions (.xans)",
                AllowMultiple = true, // CRITICAL: This allows batch selection!
                FileTypeFilter = new[] { xansFileType, FilePickerFileTypes.All }
            });

            foreach (var file in files)
            {
                selectedPaths.Add(file.Path.LocalPath);
            }
        }
        return selectedPaths;
    }

    public async Task<string?> SaveCsvFileAsync(string defaultFileName)
    {
        // Get the current window to act as the parent for the popup dialog
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window == null) return null;

            // Open the native Windows/Mac Save File Explorer
            var file = await window.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Class Roster Export",
                SuggestedFileName = defaultFileName,
                DefaultExtension = "csv",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("CSV Document") { Patterns = new[] { "*.csv" } }
                }
            });

            // Return the full path the user chose (or null if they hit Cancel)
            return file?.TryGetLocalPath();
        }
        return null;
    }
}