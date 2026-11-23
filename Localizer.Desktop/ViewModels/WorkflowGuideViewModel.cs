namespace Localizer.Desktop.ViewModels;

public sealed class WorkflowGuideViewModel : ViewModelBase
{
    public WorkflowGuideViewModel(IReadOnlyList<WorkflowSection> sections)
    {
        Sections = sections ?? throw new ArgumentNullException(nameof(sections));
    }

    public IReadOnlyList<WorkflowSection> Sections { get; }
}
