using Avalonia.Controls;
using ScriviTest.ViewModels.Examinee;

namespace ScriviTest.Views.Examinee;

public partial class ExamineeTestView : UserControl
{
    public ExamineeTestView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Attach to the Top-Level Window (OS boundaries)
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Deactivated += Window_Deactivated;
            window.Activated += Window_Activated;
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Deactivated -= Window_Deactivated;
            window.Activated -= Window_Activated;
        }
    }

    private void Window_Deactivated(object? sender, System.EventArgs e)
    {
        // Tell the ViewModel the app lost focus!
        if (DataContext is ExamineeTestViewModel vm) vm.HandleFocusLost();
    }

    private void Window_Activated(object? sender, System.EventArgs e)
    {
        // Tell the ViewModel the student returned!
        if (DataContext is ExamineeTestViewModel vm) vm.HandleFocusRegained();
    }
}