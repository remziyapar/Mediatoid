using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public sealed class GeneratedStreamTests
{
	[Fact]
	public async Task Stream_Should_Flow_Through_Behavior_On_Generated_Path()
	{
		StreamProbe.Reset();

		var services = new ServiceCollection()
			.AddMediatoid(typeof(Di).Assembly)
			.AddTransient(typeof(IStreamBehavior<,>), typeof(LoggingStreamBehavior<,>))
			.AddTransient<IStreamRequestHandler<CountTo, int>, CountToHandler>()
			.BuildServiceProvider();

		var sender = services.GetRequiredService<ISender>();

		var list = new List<int>();
		await foreach (var item in sender.Stream(new CountTo(3)))
		{
			list.Add(item);
		}

		Assert.Equal(new[] { 1, 2, 3 }, list);
		Assert.Equal(1, StreamProbe.BehaviorCallsBefore);
		Assert.Equal(1, StreamProbe.BehaviorCallsAfter);
	}
}

public sealed record CountTo(int Value) : IStreamRequest<int>;

public static class StreamProbe
{
	public static int BehaviorCallsBefore { get; private set; }
	public static int BehaviorCallsAfter { get; private set; }

	public static void Reset()
	{
		BehaviorCallsBefore = 0;
		BehaviorCallsAfter = 0;
	}

	public static void IncrementBefore() => BehaviorCallsBefore++;

	public static void IncrementAfter() => BehaviorCallsAfter++;
}

public sealed class CountToHandler : IStreamRequestHandler<CountTo, int>
{
	public async IAsyncEnumerable<int> Handle(CountTo request, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		for (var i = 1; i <= request.Value; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return i;
			await Task.Yield();
		}
	}
}

public sealed class LoggingStreamBehavior<TRequest, TItem> : IStreamBehavior<TRequest, TItem>
	where TRequest : IStreamRequest<TItem>
{
	public async IAsyncEnumerable<TItem> Handle(TRequest request, StreamHandlerContinuation<TItem> continuation, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		StreamProbe.IncrementBefore();
		await foreach (var item in continuation())
		{
			yield return item;
		}
		StreamProbe.IncrementAfter();
	}
}
