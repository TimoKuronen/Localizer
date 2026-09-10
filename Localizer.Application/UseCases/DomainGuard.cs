using Localizer.Core.Errors;

namespace Localizer.Application.UseCases;

internal static class DomainGuard
{
    public static UseCaseResult Try(Action action)
    {
        try
        {
            action();
            return UseCaseResult.Success();
        }
        catch (DomainValidationException exception)
        {
            return UseCaseResult.Failure(exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult.Failure(UseCaseErrorCodes.InvalidArgument, exception.Message);
        }
    }

    public static UseCaseResult<T> Try<T>(Func<T> action)
    {
        try
        {
            return UseCaseResult<T>.Success(action());
        }
        catch (DomainValidationException exception)
        {
            return UseCaseResult<T>.Failure(exception.Code, exception.Message);
        }
        catch (ArgumentException exception)
        {
            return UseCaseResult<T>.Failure(UseCaseErrorCodes.InvalidArgument, exception.Message);
        }
    }
}
