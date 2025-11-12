# Changelog

Tüm anlamlı değişiklikler bu dosyada listelenir. Sürümleme SemVer’e uygundur.

## [0.1.0] - 2025-11-12
### Added
- İlk sürüm (Initial release)
- Core sözleşmeler: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler
- Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable) desteği
- Pipeline davranışları (open generic) ve deterministik yürütme sırası
- DI entegrasyonu: AddMediatoid(params Assembly[])
- Basit reflection tabanlı handler keşfi
- xUnit testleri (Send/Publish/Stream/Pipeline)
