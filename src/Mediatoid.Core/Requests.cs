namespace Mediatoid;

/// <summary>Represents a request that returns a response.</summary>
public interface IRequest<TResponse> { }

/// <summary>Represents a fire-and-forget notification.</summary>
public interface INotification { }

/// <summary>Represents a streaming request that yields items.</summary>
public interface IStreamRequest<TItem> { }