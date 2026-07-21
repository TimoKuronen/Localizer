namespace Localizer.Core.Errors;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
