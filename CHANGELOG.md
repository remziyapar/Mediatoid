# Changelog

Tüm anlamlı değişiklikler bu dosyada listelenir. Sürümleme SemVer'e uygundur.

## [0.2.0] - 2025-11-15
### Added
- Send pipeline semantiği belgelendi ve donduruldu (v0.2.0).
- Yeni dokümantasyon: `docs/pipeline-semantics.md`.
- README "Pipeline Davranışları (Send)" bölümü güncellendi.

### Changed
- Pipeline sıra/compose kuralları ve kapsamı (yalnızca Send) açıkça dokümante edildi.
- Publish/Stream için pipeline olmadığı README'de netleştirildi.

### Notes
- Bu sürümde public API değişikliği yoktur (geriye dönük uyumludur).
- Performans iyileştirmeleri bir sonraki patch/minor sürümlerde ele alınacaktır.

## [0.1.0] - 2025-11-12
### Added
- İlk sürüm (Initial release)
- Core sözleşmeler: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler
- Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable) desteği
- Pipeline davranışları (open generic) ve deterministik yürütme sırası
- DI entegrasyonu: AddMediatoid(params Assembly[])
- Basit reflection tabanlı handler keşfi
- xUnit testleri (Send/Publish/Stream/Pipeline)
