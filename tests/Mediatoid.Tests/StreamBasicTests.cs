using Mediatoid;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

public sealed record Range(int Count) : IStreamRequest<int>;
public sealed class RangeHandler : IStreamRequestHandler<Range, int>
{
    public async IAsyncEnumerable<int> Handle(Range request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 1; i <= request.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

public class StreamBasicTests
{
    [Fact]
    public async Task Stream_Should_Produce_All_Items()
    {
        // Baseline senaryo: bu testte pipeline davranışlarını izole etmek için
        // stream behaviors devre dışı bırakılır; doğrudan handler üzerinden çağrılır.
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(StreamBasicTests).Assembly)
            .BuildServiceProvider();

        var handler = sp.GetRequiredService<IStreamRequestHandler<Range, int>>();
        var list = new List<int>();
        await foreach (var x in handler.Handle(new Range(3), CancellationToken.None))
            list.Add(x);

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }
}
