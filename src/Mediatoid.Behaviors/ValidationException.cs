using FluentValidation.Results;

namespace Mediatoid.Behaviors;

/// <summary>
/// Toplanmış doğrulama hatalarını temsil eder.
/// </summary>
public sealed class ValidationException(IEnumerable<ValidationFailure> failures) : Exception(BuildMessage(failures))
{
    /// <summary>
    /// Doğrulama sırasında oluşan hataların listesini alır.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; } = [.. failures];

    private static string BuildMessage(IEnumerable<ValidationFailure> failures)
        => string.Join(Environment.NewLine, failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
}
