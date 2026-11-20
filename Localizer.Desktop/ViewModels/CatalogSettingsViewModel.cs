using CommunityToolkit.Mvvm.ComponentModel;
using Localizer.Application.UseCases;
using Localizer.Core.Syntax;

namespace Localizer.Desktop.ViewModels;

public partial class CatalogSettingsViewModel : ViewModelBase
{
    public CatalogSettingsViewModel(ConfigureCatalogSettingsRequest current)
    {
        SourceLocale = current.SourceLocale;
        RequiredLocalesText = string.Join(", ", current.RequiredLocales);
        UseCompositeSyntax = current.DefaultSyntaxProfile == MessageSyntaxProfile.Composite;
        ProjectContext = current.ProjectContext ?? string.Empty;
        Glossary = current.Glossary ?? string.Empty;
    }

    [ObservableProperty]
    public partial string SourceLocale { get; set; }

    [ObservableProperty]
    public partial string RequiredLocalesText { get; set; }

    [ObservableProperty]
    public partial bool UseCompositeSyntax { get; set; }

    [ObservableProperty]
    public partial string ProjectContext { get; set; }

    [ObservableProperty]
    public partial string Glossary { get; set; }

    public ConfigureCatalogSettingsRequest ToRequest() => new()
    {
        SourceLocale = SourceLocale.Trim(),
        RequiredLocales = RequiredLocalesText
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList(),
        DefaultSyntaxProfile = UseCompositeSyntax ? MessageSyntaxProfile.Composite : MessageSyntaxProfile.Plain,
        ProjectContext = ProjectContext,
        Glossary = Glossary
    };
}
