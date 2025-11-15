# Changelog

Tüm anlamlý deðiþiklikler bu dosyada listelenir. Sürümleme SemVer’e uygundur.

## [0.2.0] - 2025-11-15
### Added
- Send pipeline semantiði belgelendi ve donduruldu (v0.2.0).
- Yeni dokümantasyon: `docs/pipeline-semantics.md`.
- README “Pipeline Davranýþlarý (Send)” bölümü güncellendi.

### Changed
- Pipeline sýra/compose kurallarý ve kapsamý (yalnýzca Send) açýkça dokümante edildi.
- Publish/Stream için pipeline olmadýðý README’de netleþtirildi.

### Notes
- Bu sürümde public API deðiþikliði yoktur (geriye dönük uyumludur).
- Performans iyileþtirmeleri bir sonraki patch/minor sürümlerde ele alýnacaktýr.

## [0.1.0] - 2025-11-12
### Added
- Ýlk sürüm (Initial release)
- Core sözleþmeler: IRequest, IRequestHandler, INotification, INotificationHandler, ISender, IPipelineBehavior, IStreamRequest, IStreamRequestHandler
- Send (Request/Response), Publish (Notification), Stream (IAsyncEnumerable) desteði
- Pipeline davranýþlarý (open generic) ve deterministik yürütme sýrasý
- DI entegrasyonu: AddMediatoid(params Assembly[])
- Basit reflection tabanlý handler keþfi
- xUnit testleri (Send/Publish/Stream/Pipeline)