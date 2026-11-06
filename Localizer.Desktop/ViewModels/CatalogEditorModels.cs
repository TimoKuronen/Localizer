using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

public partial class EntryRowViewModel : ViewModelBase
{
    public EntryRowViewModel(string key, string sourceText, string localeStatuses)
    {
        Key = key;
        SourceText = sourceText;
        LocaleStatuses = localeStatuses;
    }

    public string Key { get; }

    [ObservableProperty]
    public partial string SourceText { get; set; }

    [ObservableProperty]
    public partial string LocaleStatuses { get; set; }
}

public partial class TranslationEditViewModel : ViewModelBase
{
    private readonly Func<TranslationEditViewModel, Task>? _approveAsync;
    private readonly Func<TranslationEditViewModel, Task>? _invalidateAsync;
    private readonly Action? _textEdited;
    private TranslationEffectiveStatus _effectiveStatus;

    public TranslationEditViewModel(
        string locale,
        string text,
        TranslationEffectiveStatus status,
        Func<TranslationEditViewModel, Task>? approveAsync = null,
        Func<TranslationEditViewModel, Task>? invalidateAsync = null,
        Action? textEdited = null)
    {
        Locale = locale;
        Text = text;
        Status = status.ToString();
        OriginalText = text;
        _effectiveStatus = status;
        _approveAsync = approveAsync;
        _invalidateAsync = invalidateAsync;
        _textEdited = textEdited;
        RefreshCanApprove();
        RefreshCanInvalidate();
    }

    public string Locale { get; }

    public string OriginalText { get; private set; }

    public bool IsStale => _effectiveStatus == TranslationEffectiveStatus.Stale;

    public bool IsDraft => _effectiveStatus == TranslationEffectiveStatus.Draft;

    public bool IsApproved => _effectiveStatus == TranslationEffectiveStatus.Approved;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial bool IsReviewTarget { get; set; }

    [ObservableProperty]
    public partial bool CanApprove { get; set; }

    [ObservableProperty]
    public partial bool CanInvalidate { get; set; }

    [ObservableProperty]
    public partial string ApproveHint { get; set; } = string.Empty;

    public bool HasTextChanged =>
        !string.Equals(Text, OriginalText, StringComparison.Ordinal);

    public void Refresh(string text, TranslationEffectiveStatus status)
    {
        OriginalText = text;
        _effectiveStatus = status;
        Status = status.ToString();
        Text = text;
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(IsApproved));
        RefreshCanApprove();
        RefreshCanInvalidate();
    }

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        if (_approveAsync is null)
        {
            return;
        }

        await _approveAsync(this).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanInvalidate))]
    private async Task InvalidateAsync()
    {
        if (_invalidateAsync is null)
        {
            return;
        }

        await _invalidateAsync(this).ConfigureAwait(true);
    }

    partial void OnTextChanged(string value)
    {
        RefreshCanApprove();
        _textEdited?.Invoke();
    }

    partial void OnCanApproveChanged(bool value) => ApproveCommand.NotifyCanExecuteChanged();

    partial void OnCanInvalidateChanged(bool value) => InvalidateCommand.NotifyCanExecuteChanged();

    private void RefreshCanApprove()
    {
        CanApprove = _effectiveStatus switch
        {
            TranslationEffectiveStatus.Draft => !string.IsNullOrEmpty(Text),
            TranslationEffectiveStatus.Missing
                or TranslationEffectiveStatus.Stale
                or TranslationEffectiveStatus.Approved => HasTextChanged && !string.IsNullOrEmpty(Text),
            _ => false
        };
        ApproveHint = DescribeApproveState();
    }

    // Explains the UI gate only; blocking validation is still enforced by the approve use case.
    private string DescribeApproveState()
    {
        if (CanApprove)
        {
            return "Approve this text for release. Blocking validation errors prevent approval.";
        }

        if (string.IsNullOrEmpty(Text))
        {
            return "Enter a translation before approving.";
        }

        return _effectiveStatus switch
        {
            TranslationEffectiveStatus.Approved => "Already approved.",
            TranslationEffectiveStatus.Stale =>
                "Source changed since this text was written. Edit the text or draft it again before approving.",
            _ => "Edit the text before approving."
        };
    }

    private void RefreshCanInvalidate()
    {
        CanInvalidate = _effectiveStatus is TranslationEffectiveStatus.Draft
            or TranslationEffectiveStatus.Approved;
    }
}

public partial class DiagnosticRowViewModel : ViewModelBase
{
    public DiagnosticRowViewModel(string severity, string code, string message, string? entryKey, string? locale)
    {
        Severity = severity;
        Code = code;
        Message = message;
        EntryKey = entryKey ?? string.Empty;
        Locale = locale ?? string.Empty;
    }

    public string Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string EntryKey { get; }

    public string Locale { get; }
}

public partial class MessageViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;
}

public partial class NewCatalogViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string CatalogId { get; set; } = "catalog";

    [ObservableProperty]
    public partial string SourceLocale { get; set; } = "en";

    [ObservableProperty]
    public partial string RequiredLocalesText { get; set; } = "es";

    [ObservableProperty]
    public partial bool UseCompositeSyntax { get; set; }
}

public partial class AddEntryViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeveloperNotes { get; set; } = string.Empty;
}
