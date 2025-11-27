# Mediatoid Mimari ve Yol Haritası (v0.4.0 Odaklı)

Bu doküman, Mediatoid kütüphanesinin mimari katmanlarını, bugün sağlanan davranış sözleşmelerini ve v0.4.0 sürümü için hedeflenen geliştirmeleri özetler.

---

## 1. Mimari Katmanlar

Mediatoid, katmanlı bir yapı ile tasarlanmıştır. Her katmanın sorumluluğu nettir ve üst katmanlar alt katmanlara bağımlıdır; tersi değil.

### 1.1. `Mediatoid.Core`

- **Amaç:**
  - Temel kontratları ve soyut tipleri sağlar.
  - DI framework'lerine, Source Generator'a veya belirli bir runtime'a bağımlı değildir.
- **İçerik (örnekler):**
  - `IRequest<TResponse>`, `INotification`, `IStreamRequest<TItem>`
  - `IRequestHandler<,>`, `INotificationHandler<>`, `IStreamRequestHandler<,>`
  - `IPipelineBehavior<TRequest, TResponse>`, `RequestHandlerContinuation<TResponse>`
  - `MediatoidRootAttribute` (SourceGen için kök işaretleme)
- **İlkeler:**
  - Minimal ve kararlı API yüzeyi.
  - Geriye dönük uyumluluk önceliklidir.

### 1.2. `Mediatoid`

- **Amaç:**
  - Çalışma zamanında `ISender` implementasyonu sağlar.
  - DI üzerinden handler ve davranışların çözülmesini, pipeline compose işlemini yönetir.
- **İçerik (örnekler):**
  - `Mediator` sınıfı (`ISender` implementasyonu)
  - `ServiceCollectionExtensions.AddMediatoid(...)`
  - Reflection tabanlı handler ve pipeline keşfi
  - Deterministik pipeline compose (bkz. `pipeline-semantics.md`)
- **Rolü:**
  - Her zaman mevcut olan **referans davranış**tır.
  - SourceGen devrede olsun veya olmasın, beklenen semantiği tanımlar.

### 1.3. `Mediatoid.Behaviors` (Opsiyonel)

- **Amaç:**
  - Sık kullanılan hazır pipeline davranışlarını sağlar.
- **İçerik (bugün):**
  - Logging davranışı
  - Validation davranışı (`FluentValidation` ile entegrasyon)
- **Kullanım:**
  - Küçük projeler yalnızca `Mediatoid` kullanabilir.
  - İhtiyaç halinde `AddMediatoidBehaviors()` ile eklenir.

### 1.4. `Mediatoid.SourceGen` (Opsiyonel)

- **Amaç:**
  - Handler keşfini ve belirli pipeline parçalarını build-time'a taşıyarak runtime maliyetini azaltmak.
- **v0.3.x Durumu:**
  - Handler terminali için optimizasyon yapar.
  - Pipeline davranış zinciri (behaviors) hâlen runtime'da compose edilir.
- **Temel İlke:**
  - Fonksiyonel davranış, `Mediatoid` runtime yolu ile **eşit** olmalıdır.
  - SourceGen yalnızca performans optimizasyonu sunar; semantiği değiştirmez.

---

## 2. Davranış Sözleşmeleri (Bugünkü Durum)

Bu bölüm, kullanıcıya bugün itibarıyla garanti edilen davranışları özetler. Detaylar ilgili dokümanlarda (özellikle `pipeline-semantics.md`) yer alır.

### 2.1. Genel İlkeler

- Tek giriş noktası: `ISender` (`Send`, `Publish`, `Stream`).
- `ValueTask` tabanlı asenkron akış (allocation'ları azaltma hedefi).
- Deterministik pipeline sırası.
- Exception'lar wrap edilmeden yüzeye çıkar (fırlatılan tür korunur).

### 2.2. `Send` (Request/Response)

- Bir `Send` çağrısı için:
  - İlgili request tipine kayıtlı **bir handler** çalıştırılır.
  - Tanımlı pipeline davranışları (behaviors) dıştan içe doğru compose edilir.
- Pipeline kapsamı, sırası ve lifetime kuralları `pipeline-semantics.md` içinde tanımlıdır.

### 2.3. `Publish` (Notification)

- Kayıtlı tüm notification handler'ları çağrılır.
- Handler'lar seri şekilde çalıştırılır.
- Şu anda `Publish` için pipeline davranışı uygulanmaz.

### 2.4. `Stream` (IAsyncEnumerable)

- `IStreamRequest<TItem>` için tek bir stream handler'ı çalıştırılır.
- Cancellation ve hata davranışı doğrudan handler implementasyonuna bırakılır.
- Şu anda `Stream` için pipeline davranışı uygulanmaz.

---

## 3. Source Generator İçin Hedefler (v0.4.0 Odaklı)

### 3.1. Davranış Eşitliği

- **Hedef:**
  - SourceGen açıkken ve kapalıyken, aynı request için gözlenen davranış bire bir aynı olmalıdır.
  - Aynı request:
    - Aynı handler çağrı sayısı,
    - Aynı pipeline davranışı sırası,
    - Aynı exception/cancellation davranışını üretmelidir.
- **İlke:**
  - SourceGen yalnızca hızlandırıcı bir katmandır; semantik farklılık kabul edilmez.

### 3.2. Tek Pipeline Garantisi (Send)

- **Hedef:**
  - Bir `Send` çağrısı için **ya** generated pipeline **ya** runtime pipeline çalıştırılır.
  - Aynı request için iki pipeline'ın birden (generated + runtime) çalışması kabul edilmez.
- **Sonuç:**
  - Generated dispatch başarılı olduğunda, aynı çağrı için runtime compose yolu devreye girmez.

### 3.3. Tam Pipeline Zinciri Üretimi

- v0.4.0 serisi boyunca hedef:
  - Handler + pipeline davranış zincirinin tamamının compile-time'da üretilebilmesi.
- **Kısıt:**
  - Üretilen pipeline zinciri, runtime compose ile **aynı sıra** ve **aynı dedup** kurallarına uymalıdır.

---

## 4. Diagnostik ve Gözlemlenebilirlik

### 4.1. Pipeline Adım Olayları

- `MediatoidDiagnostics` üzerinden, pipeline adımlarının hafif ve isteğe bağlı şekilde yayımlanması hedeflenir.
- Kullanım senaryoları:
  - Testlerde path doğrulama (generated vs runtime).
  - Benchmark ve performans analizi.
  - Gelişmiş logging/metrics entegrasyonu.

### 4.2. Stabil Sözleşme

- v0.4.0 ile birlikte, diagnostic API'nin temel şekli belirlenecek ve dokümante edilecektir.
- Bu API, kırılma etkisi yüksek olduğu için değişiklikler dikkatle versiyonlanacaktır.

---

## 5. Test Stratejisi

### 5.1. Runtime Referans Testleri

- Proje: `Mediatoid.Tests` (ve ilgili alt projeler)
- Amaç:
  - Runtime compose davranışını referans kabul eden sözleşme testleri.
  - `Send` / `Publish` / `Stream` + pipeline semantiğinin doğrulanması.

### 5.2. SourceGen Sözleşme Testleri

- Proje: `Mediatoid.SourceGen.Tests`
- Amaç:
  - SourceGen yolu ile runtime yolu arasında **davranış eşitliği** sağlamak.
- Yaklaşım:
  - Aynı senaryoyu hem generated path hem runtime compose path ile koşturup sonuçları karşılaştıran testler.
  - Tek pipeline garantisini doğrulayan diagnosic tabanlı testler (ör. belirli bir request için behavior log'larının yalnızca bir kez oluştuğunu kontrol etmek).

---

## 6. v0.4.0 İçin Özet Hedefler

Bu başlık, v0.4.0 serisi (0.4.x) için odaklanılan ana işleri özetler.

1. **SourceGen Davranış Eşitliği**
   - Generated ve runtime yolları için fonksiyonel olarak aynı davranışın sağlanması.
   - Özellikle `Send` için handler/pipeline çağrı sayıları ve sırasının eşitlenmesi.

2. **Tek Pipeline Garantisi**
   - Bir `Send` çağrısında yalnızca tek bir pipeline zincirinin (generated veya runtime) çalıştırılması.

3. **Tam Pipeline Zinciri Üretimi (İlk Versiyon)**
   - Handler + behaviors için compile-time pipeline üretiminin ilk stabil versiyonu.

4. **Diagnostic API'nin Stabilizasyonu**
   - Pipeline adımlarının izlenebilirliği için hafif ve kararlı bir diagnostik sözleşme.

5. **Sözleşme Tabanlı Test Seti**
   - Runtime referans testleri ve SourceGen sözleşme testlerinin ayrıştırılması.
   - Geriye dönük uyumluluk ve regresyon kontrollerinin bu testlere dayandırılması.

6. **Diagnostics ile Zengin Davranış Eşitleme (v0.4.x+)**
  - SourceGen ile üretilen pipeline zincirinde de `MediatoidDiagnostics` üzerinden behavior adımlarının yayınlanması.
  - Generated ve runtime yollarının yalnızca sonuç değil, adım adım (before/after/handler) davranış eşitliğinin diagnostik verilerle doğrulanması.

Bu doküman, ilerleyen sürümlerde güncellenerek mimari ve roadmap kararlarının tek referans noktası olarak kullanılacaktır.

---

## 7. Geliştirici Notları / Geçmiş Sorunlar ve Kararlar

Bu bölüm, özellikle v0.3.x → v0.4.x geçişinde karşılaşılan sorunları ve alınan kararları geliştiricilere hatırlatmak için tutulur.

### 7.1. SourceGen Fast‑Path Başlatma Hatası (GeneratedDispatchCache)

- **Problem (v0.3.x):**
  - `GeneratedDispatchCache.EnsureInitialized()` ilk çağrıda `Mediatoid.Generated.MediatoidGeneratedDispatch` tipini bulamazsa bile `_initialized = true` yapıyordu.
  - Bu durum bazı test koşullarında (özellikle çoklu test projeleri ve discovery sırasında) generated tip henüz AppDomain'e yüklenmeden ilk çağrının gelmesine ve fast‑path'in kalıcı olarak devre dışı kalmasına yol açıyordu.
- **Alınan Karar:**
  - `_initialized` yalnızca `MediatoidGeneratedDispatch.TryInvoke` metodu başarıyla bulunduğunda `true` yapılır.
  - Tip ilk denemede yüklenmemişse, sonraki `Send` çağrılarında `EnsureInitialized` yeniden çalıştırılır ve tip bulunduğunda fast‑path devreye girer.
- **Etki:**
  - SourceGen paketi yüklü değilse veya jeneratör hiçbir tip üretmiyorsa, her `Send` çağrısında "bulunamadı" denetimi yeniden yapılır, ardından runtime compose yoluna düşülür.
  - Bu davranış fonksiyonel olarak doğru, ancak performans açısından ölçülmeye değerdir (özellikle SourceGen'in hiç kullanılmadığı senaryolarda).

### 7.2. Tek Pipeline Garantisi ve Testlerin Yeniden Yazılması

- **Problem:**
  - Bazı SourceGen testlerinde aynı request için hem generated pipeline hem runtime pipeline çalıştırılıyormuş gibi görünen davranışlar (örneğin behavior loglarının iki kez oluşması) tespit edildi.
  - Eski testler, geçmiş iterasyonların yan ürünü olarak, bugünkü tasarım hedefleriyle tam uyumlu olmayan beklentiler içeriyordu (örneğin belirli call‑count varsayımları).
- **Alınan Karar:**
  - `Send` için tek pipeline garantisi hedeflenir: bir çağrı için ya generated pipeline ya runtime compose çalıştırılır; ikisi birden aynı request için çalıştırılmaz.
  - 0.4.x serisinde SourceGen testleri, bu sözleşmeye göre yeniden yazılacaktır. Eski testler gerekirse tamamen silinip, yeni contract‑test seti ile değiştirilecektir.
- **Eylem Notu:**
  - SourceGen davranışı stabilize edildikten sonra, `Mediatoid.SourceGen.Tests` içinde:
    - Davranış eşitliğini (generated vs runtime) doğrulayan testler,
    - Tek pipeline garantisini diagnosic/pipeline adım olayları üzerinden doğrulayan testler,
    yeniden tasarlanmalıdır.

### 7.3. SourceGen Yokken Tekrar Deneme Stratejisinin Değerlendirilmesi

- **Durum:**
  - `GeneratedDispatchCache.EnsureInitialized()` şu an, generated tip hiç yoksa bile her `Send` çağrısında "tip var mı?" sorusunu tekrar sorar ve bulamazsa sessizce runtime compose yoluna düşer.
  - Bu, fonksiyonel olarak doğru; ancak SourceGen paketinin hiç eklenmediği uygulamalarda gereksiz bir runtime maliyeti oluşturabilir (her çağrıda assembly taraması).
- **Planlanan Adımlar:**
  - Benchmark testlerinde iki senaryonun ölçülmesi önerilir:
    1. SourceGen **yüklü** ve tip mevcut (fast‑path her zaman devrede).
    2. SourceGen **yüklü değil** veya jeneratör tip üretmiyor (her çağrıda tekrar deneme + runtime compose).
  - Bu ölçümlerin sonucuna göre, gerekirse şu tür bir optimizasyon değerlendirilebilir:
    - Belirli sayıda başarısız denemeden sonra "generated tip yok" sonucunu cache'leyip, tekrar denemeyi durdurmak.
    - Veya uygulama seviyesinde (örn. bir ayar üzerinden) SourceGen aramasını devre dışı bırakmak.
- **Not:**
  - Bu optimizasyonlar semantiği değiştirmemeli; sadece performans karakteristiğini iyileştirmeyi hedeflemelidir.

