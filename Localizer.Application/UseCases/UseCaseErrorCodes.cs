namespace Localizer.Application.UseCases;

public static class UseCaseErrorCodes
{
    public const string InvalidArgument = "usecase.invalid_argument";
    public const string EntryNotFound = "entry.not_found";
    public const string LocaleNotRequired = "translation.locale_not_required";
    public const string TranslationNotFound = "translation.not_found";
    public const string TranslationNotDraft = "translation.not_draft";
    public const string ValidationBlocked = "translation.validation_blocked";
}
