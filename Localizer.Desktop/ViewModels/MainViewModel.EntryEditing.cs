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

        if (CanEditEntry && HasPendingEditorChanges())
        {
            var previousKey = EditorKey;
            var applied = await TryApplyPendingEditorChangesAsync(refreshUi: false).ConfigureAwait(true);
            if (!applied)
            {
                // Revert selection so the user can fix validation errors before leaving the row.
                SetSelectedEntryWithoutLoad(Entries.FirstOrDefault(entry => entry.Key == previousKey));
                return;
            }

            IsDirty = true;
            StatusText = $"Saved pending edits for '{previousKey}'.";
        }

        LoadSelectedEntry(value);
    }

    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (_catalog is null)
        {
            return;
        }

        var request = await _dialogs.PromptAddEntryAsync().ConfigureAwait(true);
        if (request is null)
        {
            return;
        }

        var result = _addEntry.Execute(_catalog, request);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Add entry failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: request.Key);
        StatusText = $"Added entry '{request.Key}'.";
    }

    [RelayCommand]
    private async Task RemoveEntryAsync()
    {
        if (_catalog is null || SelectedEntry is null)
        {
            return;
        }

        var key = SelectedEntry.Key;
        var result = _removeEntry.Execute(_catalog, key);
        if (!result.Succeeded)
        {
            await _dialogs.ShowMessageAsync("Remove entry failed", FormatError(result)).ConfigureAwait(true);
            return;
        }

        IsDirty = true;
        RefreshFromCatalog(selectKey: null);
        StatusText = $"Removed entry '{key}'.";
    }

    [RelayCommand]
    private async Task ApplyEntryEditsAsync()
    {
        if (_catalog is null || !CanEditEntry || string.IsNullOrWhiteSpace(EditorKey))
        {
            return;
        }

        var key = EditorKey;
        var applied = await TryApplyPendingEditorChangesAsync(refreshUi: true).ConfigureAwait(true);
        if (!applied)
        {
            return;
        }

        StatusText = $"Applied changes to '{key}'.";
    }

    private bool HasPendingEditorChanges()
    {
        if (_catalog is null || string.IsNullOrWhiteSpace(EditorKey))
        {
            return false;
        }

        if (!_catalog.Entries.TryGetValue(EntryKey.Create(EditorKey), out var existing))
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

    // Applies editor fields through use cases, then syncs grid rows in place (no full rebuild)
    // so scroll position survives Approve/Invalidate-style status changes.
    private async Task<bool> TryApplyPendingEditorChangesAsync(bool refreshUi)
    {
        if (_catalog is null || !CanEditEntry || string.IsNullOrWhiteSpace(EditorKey))
        {
            return false;
        }

        var catalog = _catalog;
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
        SyncEntriesFromCatalog();
        if (refreshUi)
        {
            ReloadEditorAfterApply(key);
        }

        return true;
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
            await _dialogs.ShowMessageAsync("Apply failed", FormatError(updateResult)).ConfigureAwait(true);
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
                await _dialogs.ShowMessageAsync("Apply translation failed", FormatError(draftResult)).ConfigureAwait(true);
                return false;
            }
        }

        return true;
    }

    private void ReloadEditorAfterApply(string key)
    {
        var row = Entries.FirstOrDefault(entry => entry.Key == key);
        if (row is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedEntry, row))
        {
            SetSelectedEntryWithoutLoad(row);
        }

        LoadSelectedEntry(row);
        RunValidation();
    }

    private void LoadSelectedEntry(EntryRowViewModel? row)
    {
        if (_suppressSelectionLoad)
        {
            return;
        }

        if (_catalog is null || row is null)
        {
            ClearEditor();
            return;
        }

        if (!_catalog.Entries.TryGetValue(EntryKey.Create(row.Key), out var entry))
        {
            ClearEditor();
            return;
        }

        EditorKey = entry.Key.Value;
        EditorSourceText = entry.SourceText;
        EditorDeveloperNotes = entry.DeveloperNotes ?? string.Empty;
        CanEditEntry = true;

        Translations.Clear();
        foreach (var locale in _catalog.RequiredLocales.OrderBy(locale => locale.Value, StringComparer.Ordinal))
        {
            entry.Translations.TryGetValue(locale, out var translation);
            var status = _catalog.GetEffectiveStatus(entry, locale);
            Translations.Add(new TranslationEditViewModel(
                locale.Value,
                translation?.Text ?? string.Empty,
                status,
                ApproveTranslationRowAsync,
                InvalidateTranslationRowAsync));
        }
    }

    private void ClearEditor()
    {
        EditorKey = string.Empty;
        EditorSourceText = string.Empty;
        EditorDeveloperNotes = string.Empty;
        CanEditEntry = false;
        Translations.Clear();
    }
}
