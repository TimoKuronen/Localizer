using System.ComponentModel;

namespace Localizer.Desktop.ViewModels;

/// <summary>
/// A list the user ticks items on before confirming one action over the ticked ones.
/// </summary>
public sealed class ChecklistViewModel : ViewModelBase
{
    private readonly string _confirmVerb;

    public ChecklistViewModel(
        string title,
        string message,
        string confirmVerb,
        string cancelText,
        bool confirmIsDefault,
        IReadOnlyList<ChecklistItemViewModel> items)
    {
        Title = title;
        Message = message;
        _confirmVerb = confirmVerb;
        CancelText = cancelText;
        ConfirmIsDefault = confirmIsDefault;
        Items = items ?? throw new ArgumentNullException(nameof(items));
        foreach (var item in Items)
        {
            item.PropertyChanged += OnItemChanged;
        }
    }

    public string Title { get; }

    public string Message { get; }

    public string CancelText { get; }

    /// <summary>
    /// True when Enter confirms; otherwise Enter cancels.
    /// </summary>
    public bool ConfirmIsDefault { get; }

    public bool CancelIsDefault => !ConfirmIsDefault;

    public IReadOnlyList<ChecklistItemViewModel> Items { get; }

    public IReadOnlyList<string> SelectedIds => Items.Where(item => item.IsSelected).Select(item => item.Id).ToList();

    public string ConfirmText => $"{_confirmVerb} {SelectedIds.Count} selected";

    public bool CanConfirm => SelectedIds.Count > 0;

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ConfirmText));
        OnPropertyChanged(nameof(CanConfirm));
    }
}
