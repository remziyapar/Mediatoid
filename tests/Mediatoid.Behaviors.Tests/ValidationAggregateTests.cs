using Mediatoid;
using Mediatoid.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ValidationException = Mediatoid.Behaviors.ValidationException;

public sealed record Reg(string Name, string Email) : IRequest<bool>;

public sealed class RegHandler : IRequestHandler<Reg, bool>
{
    public ValueTask<bool> Handle(Reg request, CancellationToken cancellationToken) => ValueTask.FromResult(true);
}

public sealed class RegNameValidator : AbstractValidator<Reg>
{
    public RegNameValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class RegEmailValidator : AbstractValidator<Reg>
{
    public RegEmailValidator() => RuleFor(x => x.Email).EmailAddress();
}

public class ValidationAggregateTests
{
    [Fact]
    public async Task Validation_Should_Aggregate_Multiple_Failures()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(ValidationAggregateTests).Assembly)
            .AddMediatoidBehaviors()
            .AddValidatorsFromAssembly(typeof(ValidationAggregateTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new Reg("", "not-an-email")).AsTask());

        Assert.Contains(ex.Failures, f => f.PropertyName == "Name");
        Assert.Contains(ex.Failures, f => f.PropertyName == "Email");
    }
}
