# Send Pipeline Semantiği (v0.2.0)

Bu doküman, `IPipelineBehavior<TRequest, TResponse>` için çalışma kurallarını ve garanti edilen semantiği tanımlar.

## Kapsam
- Pipeline yalnızca `Send` (Request/Response) çağrıları için uygulanır.
- `Publish` (Notification) ve `Stream` (IAsyncEnumerable) için pipeline v0.2.0 sürümünde yoktur.

## Kayıt ve Sıra (Deterministik)
- Kayıt sırası DI konteynerine eklenme sırasıdır ve yürütme sırasını belirler.
- `AddMediatoid(params Assembly[])`:
  - Assembly parametrelerinin verili sırası dikkate alınır (soldan sağa).
  - Her assembly içinde, taranan tipler `Type.FullName` alfabetik (Ordinal) olarak sıralanır.
- Compose kuralı: dıştan içe. Yani önce kaydedilen behavior en dış katman olur.

## Yaşam Döngüsü (Lifetime)
- Varsayılan olarak behavior'lar ve handler'lar `Transient` kaydedilir.
- `ISender` `Scoped` olarak kaydedilir.
- İhtiyaç halinde kullanıcı kendi DI kayıtlarıyla `Scoped`/`Singleton` override edebilir.

## Open/Closed Generic Desteği
- `IPipelineBehavior<,>` open-generic olarak tanımlanabilir.
- Open-generic ve closed-generic behavior'lar birlikte çözümlebilir; kayıt sırası kuralları aynen geçerlidir.

## Short-circuit
- Bir behavior `continuation()` çağrısını atlayarak akışı sonlandırabilir ve doğrudan `TResponse` dönebilir.
- Short-circuit deterministiktir ve geçerli bir kullanımdır.

## İptal (Cancellation)
- `CancellationToken` zincirin son parametresi olarak akar.
- Behavior/handler iptal durumunda uygun şekilde `ThrowIfCancellationRequested()` çağırabilir.
- İptal istisnaları wrap edilmeden yüzeye çıkar.

## Hatalar (Exceptions)
- Behavior veya handler tarafından fırlatılan istisnalar sarmalanmadan (wrap edilmeden) aynı türde dışarıya akar.
- Yürütme sırası veya compose kuralları hata durumunda değişmez.

## Thread-safety ve Reentrancy
- Her `Send` çağrısında behavior'lar ve handler'lar yeniden çözülür (transient).
- Paylaşılan durum kullanılacaksa kullanıcı tarafında uygun senkronizasyon sağlanmalıdır.

## Sürümleme Garantisi
- Bu semantik v0.2.0 ile dondurulmuştur. Geriye dönük uyumlu kalması hedeflenir.
- Değişiklik ihtiyacı doğarsa dokümantasyon ve sürüm notlarıyla birlikte minor/major artırımı yapılır.
