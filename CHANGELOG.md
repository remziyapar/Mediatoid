# Changelog

Tüm anlamlý deðiþiklikler bu dosyada listelenir. Sürümleme SemVer’e uygundur.

## [0.1.0] - 2025-11-12
### Added
- Ýlk sürüm (Initial release)
- Core sözleþmeler: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler
- Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable) desteði
- Pipeline davranýþlarý (open generic) ve deterministik yürütme sýrasý
- DI entegrasyonu: AddMediatoid(params Assembly[])
- Basit reflection tabanlý handler keþfi
- xUnit testleri (Send/Publish/Stream/Pipeline)