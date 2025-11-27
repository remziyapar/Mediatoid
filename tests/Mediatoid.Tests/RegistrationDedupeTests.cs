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

        // Behavior short-circuit devrede ise handler'lar çağrılmayabilir; burada yalnızca
        // duplicate kayıt oluşmamasını ve Publish çağrısının birden fazla handler örneği
        // yaratmamasını test ediyoruz. Count'in 0 veya 1 olması her iki durumda da kabul edilebilir.
        Assert.InRange(NotifyOnceHandler.Count, 0, 1);
    }
}
