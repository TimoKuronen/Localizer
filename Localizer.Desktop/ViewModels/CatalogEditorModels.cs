using CommunityToolkit.Mvvm.ComponentModel;
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
    public TranslationEditViewModel(string locale, string text, TranslationEffectiveStatus status)
    {
        Locale = locale;
        Text = text;
        Status = status.ToString();
        OriginalText = text;
    }

    public string Locale { get; }

    public string OriginalText { get; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    public bool HasTextChanged =>
        !string.Equals(Text, OriginalText, StringComparison.Ordinal);
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
