using Mediatoid;
using Mediatoid.Behaviors;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed record LogPing(string Message) : IRequest<string>;

public sealed class LogPingHandler : IRequestHandler<LogPing, string>
{
    public ValueTask<string> Handle(LogPing request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Message);
}

public class LoggingBehaviorTests
{
    [Fact]
    public async Task LoggingBehavior_Should_Log_Before_After()
    {
        var logs = new List<string>();

        var services = new ServiceCollection()
            .AddLogging(b =>
            {
                b.SetMinimumLevel(LogLevel.Debug);
                b.AddProvider(new InMemoryLoggerProvider(logs));
            })
            .AddMediatoid(typeof(LoggingBehaviorTests).Assembly)
            .AddMediatoidBehaviors()
            .BuildServiceProvider();

        var sender = services.GetRequiredService<ISender>();
        _ = await sender.Send(new LogPing("x"));

        Assert.Contains(logs, l => l.Contains("Handling LogPing"));
        Assert.Contains(logs, l => l.Contains("Handled LogPing"));
    }
}

// Basit in-memory logger provider
file sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly List<string> _store;
    public InMemoryLoggerProvider(List<string> store) => _store = store;

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_store);
    public void Dispose() { }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly List<string> _store;
        public InMemoryLogger(List<string> store) => _store = store;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        // Explicit interface implementation to match nullability constraints
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _store.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}
