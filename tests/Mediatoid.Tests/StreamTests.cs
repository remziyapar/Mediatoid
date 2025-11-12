using Mediatoid;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Mediatoid.Tests;

public sealed record ListNumbers(int Count) : IStreamRequest<int>;

public sealed class ListNumbersHandler : IStreamRequestHandler<ListNumbers, int>
{
    public async IAsyncEnumerable<int> Handle(ListNumbers request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

public class StreamTests
{
    private static readonly int[] Expected = new[] { 1, 2, 3 };

    [Fact]
    public async Task StreamShouldYieldItemsInOrder()
    {
        var services = new ServiceCollection()
            .AddMediatoid(typeof(StreamTests).Assembly)
            .BuildServiceProvider();

        var sender = services.GetRequiredService<ISender>();

        var results = new List<int>();
        await foreach (var n in sender.Stream(new ListNumbers(3)))
            results.Add(n);

        Assert.Equal(Expected, results);
    }
}
