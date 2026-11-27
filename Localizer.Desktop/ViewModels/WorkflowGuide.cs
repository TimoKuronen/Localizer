namespace Localizer.Desktop.ViewModels;

/// <summary>
/// Step lists shown under Help. Menu paths are written as "Menu > Item" so a test can check them
/// against the real menu.
/// </summary>
public static class WorkflowGuide
{
    public static IReadOnlyList<WorkflowSection> All { get; } =
    [
        Section(
            "A. Set up a new project",
            "Get the source strings out of your application as a source strings JSON file (schemaVersion, sourceLocale, and entries with key and sourceText) or as a Unity String Table CSV with Key, Id and a source-language column.",
            "Choose File > New..., then enter a catalog id, the source locale, the required target locales and whether strings use composite {0} placeholders.",
            "Choose File > Save As... to store the catalog JSON, which is the authoritative file for the project.",
            "Optionally open File > Catalog Settings... to describe the product and add glossary terms so drafts use the right wording.",
            "Bring the strings in with File > Import Source Strings... for a JSON file.",
            "For a CSV, choose Project > Set Folder... on the folder that holds it, then Project > Import from Project.",
            "Start Ollama, pick a model in Review > Draft Model..., then run Review > Draft Missing/Stale to generate drafts.",
            "Select an entry, read each draft in the translation box and correct the text where needed.",
            "Press F6 to jump to the next item, F7 to edit its translation, and F9 or Approve to accept it.",
            "Fix anything listed in the Diagnostics panel, because blocking errors prevent approval.",
            "Save with Ctrl+S.",
            "Choose Export > Runtime JSON... to write the per-locale files your application reads, or Project > Export to Project to write the CSV back.",
            "To test your application before everything is approved, choose Export > Draft Preview JSON..., which also includes Draft and Stale text."),
        Section(
            "B. Edit an existing project",
            "Choose File > Open... and select the catalog JSON.",
            "If the application's strings changed, bring in the new source with File > Import Source Strings... or Project > Import from Project.",
            "New keys appear as Missing, changed source text turns its translations Stale, and Approved text is kept.",
            "Use Entry > Add Entry... or Entry > Remove Entry for single strings, or edit the source text and developer notes in the editor.",
            "When keys were deleted from the application, import the new source, draft, then choose Entry > Remove Entries Not in Source... to drop them.",
            "Run Review > Draft Missing/Stale to draft the new and Stale items.",
            "Review each item: F6 moves to the next one, F7 edits its translation, and F9 or Approve accepts it.",
            "If a Stale text still fits the changed source, press F9 twice to keep and approve it.",
            "When other entries repeat the approved source, tick the ones that should get the same text, or skip.",
            "To redo a translation that looks wrong, press Mark for Redraft on it and draft again.",
            "Review the Diagnostics panel, and use Review > Re-run Checks if you want to check again on demand.",
            "Save with Ctrl+S.",
            "Export again with Export > Runtime JSON... or Project > Export to Project.")
    ];

    private static WorkflowSection Section(string title, params string[] steps) =>
        new(title, steps.Select((text, index) => new WorkflowStep(index + 1, text)).ToList());
}
