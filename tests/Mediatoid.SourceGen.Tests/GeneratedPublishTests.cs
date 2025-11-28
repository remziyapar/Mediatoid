using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public sealed class GeneratedPublishTests
{
	[Fact]
	public async Task Publish_Should_Invoke_All_Handlers_With_Behavior_On_Generated_Path()
	{
		PublishProbe.Reset();

		var services = new ServiceCollection()
			.AddMediatoid(typeof(Di).Assembly)
			.AddTransient(typeof(INotificationBehavior<>), typeof(PublishLoggingBehavior<>))
			.AddTransient<INotificationHandler<SomethingHappened>, FirstPublishHandler>()
			.AddTransient<INotificationHandler<SomethingHappened>, SecondPublishHandler>()
			.BuildServiceProvider();

		var sender = services.GetRequiredService<ISender>();

		await sender.Publish(new SomethingHappened("ping"));

		Assert.True(PublishProbe.BehaviorCallsBefore >= 1);
		Assert.True(PublishProbe.BehaviorCallsAfter >= 1);

		Assert.True(PublishProbe.Handlers.Count >= 2);
		Assert.Contains("First:ping", PublishProbe.Handlers);
		Assert.Contains("Second:ping", PublishProbe.Handlers);
	}
}

public sealed record SomethingHappened(string Value) : INotification;

public static class PublishProbe
{
	private static readonly List<string> _handlers = new();

	public static int BehaviorCallsBefore { get; private set; }
	public static int BehaviorCallsAfter { get; private set; }

	public static IReadOnlyList<string> Handlers => _handlers;

	public static void Reset()
	{
		_handlers.Clear();
		BehaviorCallsBefore = 0;
		BehaviorCallsAfter = 0;
	}

	public static void RecordHandler(string value) => _handlers.Add(value);

	public static void IncrementBefore() => BehaviorCallsBefore++;

	public static void IncrementAfter() => BehaviorCallsAfter++;
}

public sealed class FirstPublishHandler : INotificationHandler<SomethingHappened>
{
	public ValueTask Handle(SomethingHappened notification, CancellationToken cancellationToken)
	{
		PublishProbe.RecordHandler($"First:{notification.Value}");
		return ValueTask.CompletedTask;
	}
}

public sealed class SecondPublishHandler : INotificationHandler<SomethingHappened>
{
	public ValueTask Handle(SomethingHappened notification, CancellationToken cancellationToken)
	{
		PublishProbe.RecordHandler($"Second:{notification.Value}");
		return ValueTask.CompletedTask;
	}
}

public sealed class PublishLoggingBehavior<TNotification> : INotificationBehavior<TNotification>
	where TNotification : INotification
{
	public ValueTask Handle(TNotification notification, NotificationHandlerContinuation next, CancellationToken cancellationToken)
	{
		PublishProbe.IncrementBefore();
		var task = next();
		PublishProbe.IncrementAfter();
		return task;
	}
}
