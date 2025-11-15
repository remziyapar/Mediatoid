using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record NotifyOnce(string Id) : INotification;

public sealed class NotifyOnceHandler : INotificationHandler<NotifyOnce>
{
    internal static int Count;

    public ValueTask Handle(NotifyOnce notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Count);
        return ValueTask.CompletedTask;
    }
}

public class RegistrationDedupeTests
{
    [Fact]
    public async Task AddMediatoid_WithDuplicateAssemblies_RegistersHandlersOnce()
    {
        NotifyOnceHandler.Count = 0;

        var asm = typeof(RegistrationDedupeTests).Assembly;

        var sp = new ServiceCollection()
            // Aynı assembly iki kez veriliyor → kayıtlar tekilleştirilmeli
            .AddMediatoid(asm, asm)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        await sender.Publish(new NotifyOnce("x"));

        Assert.Equal(1, NotifyOnceHandler.Count);
    }
}
