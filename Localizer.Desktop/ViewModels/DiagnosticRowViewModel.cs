using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Localizer.Core.Lifecycle;

namespace Localizer.Desktop.ViewModels;

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
