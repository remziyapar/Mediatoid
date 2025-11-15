# Send Pipeline Semantiði (v0.2.0)

Bu doküman, `IPipelineBehavior<TRequest, TResponse>` için çalýþma kurallarýný ve garanti edilen semantiði tanýmlar.

## Kapsam
- Pipeline yalnýzca `Send` (Request/Response) çaðrýlarý için uygulanýr.
- `Publish` (Notification) ve `Stream` (IAsyncEnumerable) için pipeline v0.2.0 sürümünde yoktur.

## Kayýt ve Sýra (Deterministik)
- Kayýt sýrasý DI konteynerine eklenme sýrasýdýr ve yürütme sýrasýný belirler.
- `AddMediatoid(params Assembly[])`:
  - Assembly parametrelerinin verili sýrasý dikkate alýnýr (soldan saða).
  - Her assembly içinde, taranan tipler `Type.FullName` alfabetik (Ordinal) olarak sýralanýr.
- Compose kuralý: “dýþtan içe”. Yani önce kaydedilen behavior en dýþ katman olur.

## Yaþam Döngüsü (Lifetime)
- Varsayýlan olarak behavior’lar ve handler’lar `Transient` kaydedilir.
- `ISender` `Scoped` olarak kaydedilir.
- Ýhtiyaç halinde kullanýcý kendi DI kayýtlarýyla `Scoped`/`Singleton` override edebilir.

## Open/Closed Generic Desteði
- `IPipelineBehavior<,>` open-generic olarak tanýmlanabilir.
- Open-generic ve closed-generic behavior’lar birlikte çözümlenebilir; kayýt sýrasý kurallarý aynen geçerlidir.

## Short-circuit
- Bir behavior `continuation()` çaðrýsýný atlayarak akýþý sonlandýrabilir ve doðrudan `TResponse` dönebilir.
- Short-circuit deterministiktir ve geçerli bir kullanýmdýr.

## Ýptal (Cancellation)
- `CancellationToken` zincirin son parametresi olarak akar.
- Behavior/handler iptal durumunda uygun þekilde `ThrowIfCancellationRequested()` çaðýrabilir.
- Ýptal istisnalarý wrap edilmeden yüzeye çýkar.

## Hatalar (Exceptions)
- Behavior veya handler tarafýndan fýrlatýlan istisnalar sarmalanmadan (wrap edilmeden) ayný türde dýþarýya akar.
- Yürütme sýrasý veya compose kurallarý hata durumunda deðiþmez.

## Thread-safety ve Reentrancy
- Her `Send` çaðrýsýnda behavior’lar ve handler’lar yeniden çözülür (transient).
- Paylaþýlan durum kullanýlacaksa kullanýcý tarafýnda uygun senkronizasyon saðlanmalýdýr.

## Sürümleme Garantisi
- Bu semantik v0.2.0 ile dondurulmuþtur. Geriye dönük uyumlu kalmasý hedeflenir.
- Deðiþiklik ihtiyacý doðarsa dokümantasyon ve sürüm notlarýyla birlikte minor/major artýrýmý yapýlýr.