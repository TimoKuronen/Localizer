using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Desktop.Tests.Fakes;
using Localizer.Desktop.ViewModels;
using Localizer.Desktop.Views;
using Localizer.Infrastructure.Persistence.Json;

namespace Localizer.Desktop.Tests;

/// <summary>
/// Hosts the real main window headlessly on a catalog file, so tests exercise key routing and focus.
/// </summary>
internal sealed class ReviewWindowHarness : IDisposable
{
    private static readonly TimeSpan PumpTimeout = TimeSpan.FromSeconds(10);

    private readonly string _directory;
    private readonly JsonCatalogStore _store = new();
    private readonly RecordingDialogs _dialogs = new();

    private ReviewWindowHarness(Catalog catalog)
    {
        _directory = Directory.CreateTempSubdirectory("localizer-desktop-tests-").FullName;
        CatalogPath = Path.Combine(_directory, "catalog.json");

        var save = new SaveCatalogUseCase(_store);
        var saved = Task.Run(() => save.ExecuteAsync(CatalogPath, catalog)).GetAwaiter().GetResult();
        Assert.That(saved.Succeeded, Is.True, saved.ErrorMessage);

        _dialogs.OpenPath = CatalogPath;
        ViewModel = CreateViewModel();
        Window = new MainWindow { DataContext = ViewModel };
        Window.Show();

        ViewModel.OpenCatalogCommand.Execute(null);
        PumpUntil(() => ViewModel.Entries.Count > 0);
    }

    public string CatalogPath { get; }

    public MainViewModel ViewModel { get; }

    public MainWindow Window { get; }

    public RecordingDialogs Dialogs => _dialogs;

    public IReadOnlyList<string> Messages => _dialogs.Messages;

    public string? SelectedKey => ViewModel.SelectedEntry?.Key;

    public string? ReviewTarget =>
        ViewModel.Translations.FirstOrDefault(translation => translation.IsReviewTarget)?.Locale;

    public object? FocusedElement => Window.FocusManager?.GetFocusedElement();

    public DataGrid EntriesGrid => Window.FindControl<DataGrid>("EntriesGrid")!;

    public static ReviewWindowHarness Open(Catalog catalog) => new(catalog);

    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Pump();
    }

    public void ClickTranslationText(string locale) => Click(TranslationTextBox(locale));

    public void ClickApprove(string locale) =>
        Click(Window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Content as string == "Approve"
                && button.DataContext is TranslationEditViewModel row
                && row.Locale == locale));

    public TextBox TranslationTextBox(string locale) =>
        Window.GetVisualDescendants()
            .OfType<TextBox>()
            .Single(box => box.DataContext is TranslationEditViewModel row && row.Locale == locale);

    public TranslationEditViewModel Translation(string locale) =>
        ViewModel.Translations.Single(translation => translation.Locale == locale);

    public Catalog ReadSavedCatalog()
    {
        var open = new OpenCatalogUseCase(_store);
        var result = Task.Run(() => open.ExecuteAsync(CatalogPath)).GetAwaiter().GetResult();
        Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        return result.Value!;
    }

    public void PumpUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + PumpTimeout;
        while (!condition())
        {
            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "Timed out waiting for the window.");
            Pump();
            Thread.Sleep(1);
        }

        Pump();
    }

    public void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    public void Dispose()
    {
        Window.Close();
        Directory.Delete(_directory, recursive: true);
    }

    private MainViewModel CreateViewModel() =>
        MainViewModelFactory.Create(
            _dialogs,
            _store,
            new InMemoryProjectFolderSettings(),
            new ScriptedDraftProvider());

    private void Click(Control control)
    {
        Pump();
        control.BringIntoView();
        Pump();
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        var point = control.TranslatePoint(center, Window)
            ?? throw new InvalidOperationException("Control is not in the window.");

        Window.MouseMove(point);
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Pump();
    }
}
