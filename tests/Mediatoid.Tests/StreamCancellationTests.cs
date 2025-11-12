using Mediatoid;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace Mediatoid.Tests;

public sealed record CountTo(int N) : IStreamRequest<int>;

public sealed class CountToHandler : IStreamRequestHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(CountTo request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (var i = 1; i <= request.N; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

public class StreamCancellationTests
{
    private static readonly int[] ExpectedFirst5 = new[] { 1, 2, 3, 4, 5 };

    [Fact]
    public async Task StreamStopsOnCancellation()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(StreamCancellationTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        using var cts = new CancellationTokenSource();

        var results = new List<int>();
        try
        {
            await foreach (var i in sender.Stream(new CountTo(100)).WithCancellation(cts.Token))
            {
                results.Add(i);
                if (i == 5) cts.Cancel();
            }
        }
        catch (OperationCanceledException) { /* beklenen */ }

        Assert.Equal(ExpectedFirst5, results);
    }
}
