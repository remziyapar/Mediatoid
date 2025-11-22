using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mediatoid;
using Mediatoid.Behaviors;
using System.Runtime.CompilerServices;

/// <summary>
/// Entry point: runs mediator benchmarks.
/// </summary>
internal static class Program
{
    //private static void Main() => BenchmarkRunner.Run<MediatorBenchmarks>();
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

/// <summary>Basit ping isteği.</summary>
internal sealed record Ping(string Message) : IRequest<string>;

/// <summary><see cref="Ping"/> isteğini yanıtlayan handler.</summary>
internal sealed class PingHandler : IRequestHandler<Ping, string>
{
    /// <inheritdoc />
    public ValueTask<string> Handle(Ping request, CancellationToken ct)
        => ValueTask.FromResult(request.Message);
}

/// <summary>Kullanıcı oluşturma isteği (validasyon örneği).</summary>
internal sealed record CreateUser(string Name, int Age) : IRequest<bool>;

/// <summary><see cref="CreateUser"/> isteğini işleyen handler.</summary>
internal sealed class CreateUserHandler : IRequestHandler<CreateUser, bool>
{
    /// <inheritdoc />
    public ValueTask<bool> Handle(CreateUser request, CancellationToken ct)
        => ValueTask.FromResult(true);
}

/// <summary><see cref="CreateUser"/> için FluentValidation doğrulayıcısı.</summary>
internal sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    /// <summary>Kurallar.</summary>
    public CreateUserValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
    }
}

/// <summary>Kullanıcı login bildirimi.</summary>
internal sealed record UserLoggedIn(string Name) : INotification;

/// <summary>Login bildirimi handler A.</summary>
internal sealed class UserLoggedInHandlerA : INotificationHandler<UserLoggedIn>
{
    /// <inheritdoc />
    public ValueTask Handle(UserLoggedIn notification, CancellationToken ct) => ValueTask.CompletedTask;
}

/// <summary>Login bildirimi handler B.</summary>
internal sealed class UserLoggedInHandlerB : INotificationHandler<UserLoggedIn>
{
    /// <inheritdoc />
    public ValueTask Handle(UserLoggedIn notification, CancellationToken ct) => ValueTask.CompletedTask;
}

/// <summary>Akış demo isteği (0..Count-1).</summary>
internal sealed record Range(int Count) : IStreamRequest<int>;

/// <summary><see cref="Range"/> akışını üreten handler.</summary>
internal sealed class RangeHandler : IStreamRequestHandler<Range, int>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<int> Handle(Range request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < request.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

/// <summary>Mediatoid performans karşılaştırmaları.</summary>
[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(15)]
public class MediatorBenchmarks
{
    private IServiceProvider _spBase = default!;
    private IServiceProvider _spBehaviors = default!;
    private ISender _baseSender = default!;
    private ISender _behaviorSender = default!;

    private readonly Ping _ping = new("hello");
    private readonly CreateUser _userValid = new("ali", 21);
    private readonly UserLoggedIn _notif = new("ali");
    private readonly Range _range = new(10);

    /// <summary>DI konteynerlerini hazırlar.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _spBase = new ServiceCollection()
            .AddMediatoid(typeof(MediatorBenchmarks).Assembly)
            .BuildServiceProvider();

        _spBehaviors = new ServiceCollection()
            .AddMediatoid(typeof(MediatorBenchmarks).Assembly)
            .AddValidatorsFromAssembly(typeof(MediatorBenchmarks).Assembly)
            .AddMediatoidBehaviors()
            .BuildServiceProvider();

        _baseSender = _spBase.GetRequiredService<ISender>();
        _behaviorSender = _spBehaviors.GetRequiredService<ISender>();
    }

    /// <summary>Baseline: sade handler.</summary>
    [Benchmark(Baseline = true)]
    public ValueTask<string> Send_Baseline() => _baseSender.Send(_ping);

    /// <summary>Logging behavior eklenmiş send.</summary>
    [Benchmark]
    public ValueTask<string> Send_WithLogging() => _behaviorSender.Send(new Ping("log"));

    /// <summary>Validation behavior eklenmiş send (geçerli istek).</summary>
    [Benchmark]
    public ValueTask<bool> Send_WithValidation() => _behaviorSender.Send(_userValid);

    /// <summary>İki notification handler çağrısı.</summary>
    [Benchmark]
    public async ValueTask Publish_TwoHandlers()
    {
        await _baseSender.Publish(_notif);
    }

    /// <summary>Stream ilk 10 öğeyi enumerate.</summary>
    [Benchmark]
    public async ValueTask Stream_First10()
    {
        int sum = 0;
        await foreach (var i in _baseSender.Stream(_range))
            sum += i;
    }
}
