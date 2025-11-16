using Mediatoid;
using Mediatoid.Behaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ValidationException = Mediatoid.Behaviors.ValidationException;

public sealed record CreateUser(string Name, int Age) : IRequest<string>;
public sealed class CreateUserHandler : IRequestHandler<CreateUser, string>
{
    public ValueTask<string> Handle(CreateUser request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Name);
}
public sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
    }
}

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Validation_Should_Pass_For_Valid_Request()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(ValidationBehaviorTests).Assembly)
            .AddMediatoidBehaviors()
            .AddValidatorsFromAssembly(typeof(ValidationBehaviorTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var res = await sender.Send(new CreateUser("Ali", 21));
        Assert.Equal("Ali", res);
    }

    [Fact]
    public async Task Validation_Should_Fail_For_Invalid_Request()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(ValidationBehaviorTests).Assembly)
            .AddMediatoidBehaviors()
            .AddValidatorsFromAssembly(typeof(ValidationBehaviorTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateUser("", 10)).AsTask());
        Assert.Contains("Name", ex.Message);
        Assert.Contains("Age", ex.Message);
    }
}
