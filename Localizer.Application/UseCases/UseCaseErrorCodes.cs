namespace Localizer.Application.UseCases;

public static class UseCaseErrorCodes
{
    public const string InvalidArgument = "usecase.invalid_argument";
    public const string EntryNotFound = "entry.not_found";
    public const string LocaleNotRequired = "translation.locale_not_required";
    public const string TranslationNotFound = "translation.not_found";
    public const string TranslationNotDraft = "translation.not_draft";
    public const string ValidationBlocked = "translation.validation_blocked";
    public const string ImportLocaleMismatch = "import.locale_mismatch";
    public const string ProjectFolderNotConfigured = "project.folder_not_configured";
    public const string ProjectFileNotFound = "project.file_not_found";
    public const string DraftProviderFailed = "draft.provider_failed";
    public const string DraftProviderInvalidResponse = "draft.provider_invalid_response";
}
