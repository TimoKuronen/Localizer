using CommunityToolkit.Mvvm.Input;
using Localizer.Application.UseCases;
using Localizer.Core.Catalogs;
using Localizer.Core.Identity;

namespace Localizer.Desktop.ViewModels;

public partial class MainViewModel
{
    partial void OnSelectedEntryChanged(EntryRowViewModel? value) =>
        _ = HandleSelectedEntryChangedAsync(value);

    private async Task HandleSelectedEntryChangedAsync(EntryRowViewModel? value)
    {
        if (_suppressSelectionLoad)
        {
            return;
        }

        var previousKey = EditorKey;
        if (!await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            // Revert selection so the user can fix validation errors before leaving the row.
            SetSelectedEntryWithoutLoad(Entries.FirstOrDefault(entry => entry.Key == previousKey));
            return;
        }

        LoadSelectedEntry(value);
    }

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (_session.Catalog is null)
        {
            return;
        }

        var request = await _dialogs.PromptAddEntryAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        if (!await CommitPendingEditsAsync().ConfigureAwait(true))
        {
            return;
        }

        var result = _addEntry.Execute(_session.Catalog, request);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Add entry failed", UseCaseErrorText.Format(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: request.Key);
        StatusText = $"Added entry '{request.Key}'.";
    }

    [RelayCommand]
    private async Task RemoveEntryAsync()
    {
        if (_session.Catalog is null || SelectedEntry is null)
        {
            return;
        }

        var key = SelectedEntry.Key;
        var result = _removeEntry.Execute(_session.Catalog, key);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Remove entry failed", UseCaseErrorText.Format(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: null);
        StatusText = $"Removed entry '{key}'.";
    }

    private bool HasPendingEditorChanges()
    {
        if (_session.Catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return false;
        }

        if (!_session.Catalog.Entries.TryGetValue(EntryKey.Create(EditorKey), out var existing))
        {
            return false;
        }

        if (!string.Equals(EditorSourceText, existing.SourceText, StringComparison.Ordinal))
        {
            return true;
        }

        var notes = string.IsNullOrWhiteSpace(EditorDeveloperNotes) ? null : EditorDeveloperNotes;
        if (!string.Equals(notes, existing.DeveloperNotes, StringComparison.Ordinal))
        {
            return true;
        }

        return Translations.Any(translation => translation.HasTextChanged);
    }

    // Catalog-wide actions call this first so they act on the text shown in the editor.
    private async Task<bool> CommitPendingEditsAsync()
    {
        if (_session.Catalog is null || !CanEditEntry || !HasPendingEditorChanges())
        {
            return true;
        }

        var catalog = _session.Catalog;
        var key = EditorKey;

        if (!await TryUpdateEntryFromEditorAsync(catalog, key).ConfigureAwait(true))
        {
            return false;
        }

        if (!await TryApplyChangedTranslationDraftsAsync(catalog, key).ConfigureAwait(true))
        {
            return false;
        }

        IsDirty = true;
        RefreshEditorInPlace();
        StatusText = $"Applied edits to '{key}'.";
        return true;
    }

    // Updates existing rows rather than rebuilding them, which would drop keyboard focus
    // from the editor and reset the grid scroll position.
    private void RefreshEditorInPlace()
    {
        RefreshFromCatalog(selectKey: SelectedEntry?.Key, reloadEditor: false);
        RefreshTranslationRows();
        RunValidation();
    }

    private void RefreshTranslationRows()
    {
        if (_session.Catalog is null
            || string.IsNullOrWhiteSpace(EditorKey)
            || !_session.Catalog.Entries.TryGetValue(EntryKey.Create(EditorKey), out var entry))
        {
            return;
        }

        foreach (var row in Translations)
        {
            var locale = Locale.Create(row.Locale);
            entry.Translations.TryGetValue(locale, out var translation);
            row.Refresh(translation?.Text ?? string.Empty, _session.Catalog.GetEffectiveStatus(entry, locale));
        }
    }

    private async Task<bool> TryUpdateEntryFromEditorAsync(Catalog catalog, string key)
    {
        if (!catalog.Entries.TryGetValue(EntryKey.Create(key), out var existing))
        {
            await _dialogs.ShowMessageAsync("Apply failed", $"No entry with key '{key}' exists.").ConfigureAwait(true);
            return false;
        }

        var updateResult = _updateEntry.Execute(catalog, new UpdateCatalogEntryRequest
        {
            Key = key,
            SourceText = EditorSourceText,
            Category = existing.Category,
            AllowEmptyText = existing.AllowEmptyText,
            DeveloperNotes = string.IsNullOrWhiteSpace(EditorDeveloperNotes)
                ? null
                : EditorDeveloperNotes,
            Context = existing.Context,
            Constraints = existing.Constraints,
            SyntaxProfileOverride = existing.SyntaxProfileOverride,
            ExternalIds = existing.ExternalIds
        });

        if (!updateResult.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Apply failed", UseCaseErrorText.Format(updateResult)).ConfigureAwait(true);
            return false;
        }

        return true;
    }

    private async Task<bool> TryApplyChangedTranslationDraftsAsync(Catalog catalog, string key)
    {
        if (!catalog.Entries.TryGetValue(EntryKey.Create(key), out var existing))
        {
            return false;
        }

        foreach (var translation in Translations)
        {
            if (!translation.HasTextChanged)
            {
                continue;
            }

            if (string.IsNullOrEmpty(translation.Text) && !existing.AllowEmptyText)
            {
                continue;
            }

            var draftResult = _setTranslationDraft.Execute(catalog, new SetTranslationDraftRequest
            {
                Key = key,
                Locale = translation.Locale,
                Text = translation.Text
            });

            if (!draftResult.Succeeded)
            {
                await _dialogs.ShowMessageAsync("Apply translation failed", UseCaseErrorText.Format(draftResult)).ConfigureAwait(true);
                return false;
            }
        }

        return true;
    }

    private void LoadSelectedEntry(EntryRowViewModel? row)
    {
        if (_suppressSelectionLoad)
        {
            return;
        }

        if (_session.Catalog is null || row is null)
        {
            ClearEditor();
            return;
        }

        if (!_session.Catalog.Entries.TryGetValue(EntryKey.Create(row.Key), out var entry))
        {
            ClearEditor();
            return;
        }

        EditorKey = entry.Key.Value;
        EditorSourceText = entry.SourceText;
        EditorDeveloperNotes = entry.DeveloperNotes ?? string.Empty;
        CanEditEntry = true;

        Translations.Clear();
        foreach (var locale in _session.Catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
        {
            entry.Translations.TryGetValue(locale, out var translation);
            var status = _session.Catalog.GetEffectiveStatus(entry, locale);
            Translations.Add(new TranslationEditViewModel(
                locale.Value,
                translation?.Text ?? string.Empty,
                status,
                ApproveTranslationRowAsync,
                InvalidateTranslationRowAsync,
                OnEditorContentChanged));
        }

        SelectReviewTarget(Translations.FirstOrDefault(translation => translation.IsDraft)?.Locale);
        OnEditorContentChanged();
    }

    private void ClearEditor()
    {
        EditorKey = string.Empty;
        EditorSourceText = string.Empty;
        EditorDeveloperNotes = string.Empty;
        CanEditEntry = false;
        Translations.Clear();
        SelectReviewTarget(null);
        OnEditorContentChanged();
    }
}
