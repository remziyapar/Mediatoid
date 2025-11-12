namespace Mediatoid.Pipeline;

/// <summary>
/// Send isteğinin işlenmesi sırasında araya girip çapraz kesit davranışlar (ör. logging, validation) eklemek için kullanılır.
/// Kayıt sırasına göre dıştan içe doğru zincir kurulur.
/// </summary>
/// <typeparam name="TRequest">İstek tipi.</typeparam>
/// <typeparam name="TResponse">Yanıt tipi.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : Mediatoid.IRequest<TResponse>
{
    /// <summary>
    /// Davranışı uygular. Devam etmek için <paramref name="continuation"/> delegesini çağır.
    /// </summary>
    /// <param name="request">İşlenecek istek.</param>
    /// <param name="continuation">Zincirdeki bir sonraki adım (bir sonraki behavior veya nihai handler).</param>
    /// <param name="cancellationToken">İşlemin iptali için token (her zaman sonda).</param>
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Pipeline zincirindeki bir sonraki adımı temsil eden terminal veya ara delegedir.
/// </summary>
/// <typeparam name="TResponse">Yanıt tipi.</typeparam>
public delegate ValueTask<TResponse> RequestHandlerContinuation<TResponse>();
