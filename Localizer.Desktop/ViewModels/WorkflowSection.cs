namespace Localizer.Desktop.ViewModels;

public sealed record WorkflowSection(string Title, IReadOnlyList<WorkflowStep> Steps);
