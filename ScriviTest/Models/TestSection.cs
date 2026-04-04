using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ScriviTest.Models;

public partial class TestSection : ObservableObject
{
    [ObservableProperty]
    private string _title = "New Section";

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private bool _shuffleQuestions = false;

    // The new live sub-total for this specific section
    [ObservableProperty]
    private int _sectionTotalPoints = 0;

    public ObservableCollection<Question> Questions { get; } = new();

    public TestSection()
    {
        // Tell the section to listen for whenever a Question is added or removed
        Questions.CollectionChanged += Questions_CollectionChanged;
    }

    private void Questions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // If a new Question is added, subscribe to its individual property changes (like Points changing)
        if (e.NewItems != null)
        {
            foreach (Question q in e.NewItems)
                q.PropertyChanged += Question_PropertyChanged;
        }
        
        // If a Question is deleted, unsubscribe to prevent memory leaks
        if (e.OldItems != null)
        {
            foreach (Question q in e.OldItems)
                q.PropertyChanged -= Question_PropertyChanged;
        }
        
        RecalculatePoints();
    }

    private void Question_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // If the Examiner clicked the Point spinner, recalculate the section total
        if (e.PropertyName == nameof(Question.Points))
        {
            RecalculatePoints();
        }
    }

    private void RecalculatePoints()
    {
        // Sum up all the points in this section and update the UI
        SectionTotalPoints = Questions.Sum(q => q.Points);
    }
}