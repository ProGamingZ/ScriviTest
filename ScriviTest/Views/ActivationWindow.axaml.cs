using Avalonia.Controls;
using Avalonia.Interactivity;
using UniversityScheduler.Data; // Ensure this points to your LicenseManager namespace

namespace ScriviTest.Views;

public partial class ActivationWindow : Window
{
    public ActivationWindow()
    {
        InitializeComponent();
        
        // 1. Load the Hardware ID into the read-only box
        RequestCodeBox.Text = LicenseManager.GetInstallationId();
        
        // 2. Attach the click event
        ActivateButton.Click += ActivateButton_Click;
    }

    private void ActivateButton_Click(object? sender, RoutedEventArgs e)
    {
        string inputKey = InputKeyBox.Text?.Trim() ?? "";

        // 3. Try to activate
        if (LicenseManager.TryActivate(inputKey))
        {
            // Success! Close the window and return true
            Close(true); 
        }
        else
        {
            // Fail! Show error text
            ErrorText.Text = "Activation Failed. Invalid key for this computer.";
            ErrorText.IsVisible = true;
        }
    }
}