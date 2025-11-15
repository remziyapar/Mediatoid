# Changelog

TÃ¼m anlamlÄ± deÄŸiÅŸiklikler bu dosyada listelenir. SÃ¼rÃ¼mleme SemVerâ€™e uygundur.

## [0.2.0] - 2025-11-15
### Added
- Send pipeline semantiği belgelendi ve donduruldu (v0.2.0).
- Yeni dokümantasyon: `docs/pipeline-semantics.md`.
- README “Pipeline Davranışları (Send)” bölümü güncellendi.

### Changed
- Pipeline sıra/compose kuralları ve kapsamı (yalnızca Send) açıkça dokümante edildi.
- Publish/Stream için pipeline olmadığı README’de netleştirildi.

### Notes
- Bu sürümde public API değişikliği yoktur (geriye dönük uyumludur).
- Performans iyileştirmeleri bir sonraki patch/minor sürümlerde ele alınacaktır.

## [0.1.0] - 2025-11-12
### Added
- Ä°lk sÃ¼rÃ¼m (Initial release)
- Core sÃ¶zleÅŸmeler: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler
- Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable) desteÄŸi
- Pipeline davranÄ±ÅŸlarÄ± (open generic) ve deterministik yÃ¼rÃ¼tme sÄ±rasÄ±
- DI entegrasyonu: AddMediatoid(params Assembly[])
- Basit reflection tabanlÄ± handler keÅŸfi
- xUnit testleri (Send/Publish/Stream/Pipeline)
