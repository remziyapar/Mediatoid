using FluentValidation;
using FluentValidation.Results;
using Mediatoid.Pipeline;

namespace Mediatoid.Behaviors;

/// <summary>
/// FluentValidation validator'larını çalıştırır; hatalıysa <see cref="ValidationException"/> fırlatır.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Func<IValidator<TRequest>, TRequest, CancellationToken, Task<ValidationResult>> InvokeAsync
        = static (validator, request, ct) => validator.ValidateAsync(request, ct);

    private readonly IValidator<TRequest>[] _validators = validators?.ToArray() ?? Array.Empty<IValidator<TRequest>>();

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        if (_validators.Length == 0)
            return await continuation().ConfigureAwait(false);

        List<ValidationFailure>? failures = null;

        foreach (var v in _validators)
        {
            var result = await InvokeAsync(v, request, cancellationToken).ConfigureAwait(false);
            if (result is null || result.IsValid)
                continue;

            failures ??= new List<ValidationFailure>(capacity: 8);
            failures.AddRange(result.Errors);
        }

        if (failures is not null && failures.Count > 0)
            throw new ValidationException(failures);

        return await continuation().ConfigureAwait(false);
    }
}
