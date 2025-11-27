using FluentValidation.Results;

namespace Mediatoid.Behaviors;

/// <summary>
/// Represents aggregated validation errors.
/// </summary>
public sealed class ValidationException(IEnumerable<ValidationFailure> failures) : Exception(BuildMessage(failures))
{
    /// <summary>
    /// Gets the list of errors that occurred during validation.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; } = [.. failures];

    private static string BuildMessage(IEnumerable<ValidationFailure> failures)
        => string.Join(Environment.NewLine, failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
}
