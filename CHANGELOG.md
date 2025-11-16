# Changelog
Tüm anlamlı değişiklikler bu dosyada listelenir. Sürümleme SemVer'e uygundur.

## [0.3.0] - 2025-11-18
### Added
- Paket: Mediatoid.Behaviors (LoggingBehavior, ValidationBehavior).
- Paket: Mediatoid.SourceGen (handler discovery için source generator; runtime reflection taraması handler kayıtlarında büyük ölçüde azaltıldı).
- Yeni testler: ValidationAggregateTests, ExceptionPassthroughTests, LoggingNoLoggerTests, StreamBasicTests.
- Validation: Birden çok validator hatası aggregate edilip `Mediatoid.Behaviors.ValidationException` olarak fırlatılır.

### Changed
- LoggingBehavior: ILogger yoksa sessiz çalışır (parametresiz ctor).
- DI kayıtları: Behaviors open-generic `IPipelineBehavior<,>` altında transient olarak eklenir.
- Handler çağrıları: SourceGen varsa registry üzerinden yapılır; yoksa mevcut delegate cache fallback.

### Notes
- Pipeline zinciri (behavior compose) hâlâ runtime’da deterministik (assembly parametre sırası + FullName ordinal). SourceGen şu sürümde yalnızca handler invoker optimizasyonu sağlar.
- Public API (Core) değişmedi.
- Bir sonraki aşama (v0.4.0): Tam pipeline invoker zincirinin generator ile üretimi + manifest tasarımı + benchmark dokümantasyonu.

## [0.2.1] - 2025-11-16
### Changed
- Performans: Reflection `MethodInfo` erişimleri cache'lendi (Send/Publish/Stream).
- Pipeline compose: LINQ `Reverse()` kaldırıldı; tahsis azaltıldı. Behavior yoksa kısa yol ile handler'a geçiş.
- DI tarama: Aynı assembly tekrar verildiğinde tekilleştirme; `ReflectionTypeLoadException` durumunda yüklenebilen tiplerle devam.

### Notes
- Public API değişmedi (geriye dönük uyumlu).

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
