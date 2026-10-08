# AI_LOG

Bu dosya, uygulama çalışmasında yapay zekâ araçlarının nasıl kullanıldığını kaydeder. Yalnızca gerçekten yapılmış işler yazılır. İnsanın henüz yapmadığı incelemeler ve testler yapılmış gibi gösterilmez.

## Araçlar ve roller

| Kim / araç | Rol |
| --- | --- |
| Görkem Çetinkaya (aday) | Gereksinimleri ve teknik kararları belirler, her aşamanın sonunda uygulamayı kullanarak kontrol eder |
| Codex | Gereksinimlerin ve teknik kararların belirlenmesinde adayla birlikte çalışır, her aşamanın sonunda kodu ve testleri inceler |
| Claude Code (model: Claude Opus 5.5) | Uygulama kodunu, testleri ve belgeleri yazar; komut satırı ve tarayıcı kontrollerini yapar |

## Hazırlık (sayaç başlamadan önce)

- Proje klasörü, `main` dalında henüz commit içermeyen Git deposu ve yerel Git kimliği aday tarafından hazırlandı.
- Adayın bildirdiğine göre bulut hesapları (Render ve Neon) hazırlıkta oluşturuldu. Bu aşamada bu hesaplara bağlanılmadı.
- Aday, fikir, kapsam ve teknik yaklaşım için Claude'dan en fazla 500 kelimelik bir ikinci görüş aldı. Bu konuşmada kod yazılmadı, dosya oluşturulmadı ve kurulum yapılmadı.
  - Claude'un önerileri: fikri tek bir hedef kitleye ve tek bir soruna daraltmak; ürüne LLM özelliği eklemeyi ertelemek; ASP.NET Core Minimal API ile `wwwroot` kullanmak; PostgreSQL'i yerelde Docker Compose, canlıda Neon ile çalıştırmak; Render'da yayımlamak; kalıcılığı redeploy sonrasında kontrol etmek. Ayrıca hız sınırı ve commit SHA'sını gösteren bir `/health` uç noktası da önerilmişti; bunlar 1. aşamada uygulanmadı.
  - Nihai gereksinimler ve teknik kararlar aday ve Codex tarafından verildi. İkinci görüşten farklı olarak veri erişimi için EF Core ya da Dapper yerine Npgsql ve elle yazılmış parametreli SQL seçildi. Hedef kitle "müşteri taleplerini e-posta ile Excel arasında elle taşıyan küçük işletmeler" olarak belirlendi. `ai-triage` hizmetinin yalnızca tanıtılmasına karar verildi.
- Claude, sayaç başlamadan önce görevi anladığını bildirdi ve birkaç varsayım sundu: UUID kimlik, e-postada yalnızca baştaki ve sondaki boşlukların kırpılması, Türkçe belgeler, `main` dalına commit ve `Co-Authored-By` satırı. Aday bu varsayımları onayladı.

## Aşama 1: Temel akış (sayaç başladıktan sonra)

Aday sayacı başlattığını bildirdi. Claude ilk komutu 2026-10-08 13:00:24 (+03) saatinde çalıştırdı. Uygulama geliştirmesinin tamamı bu andan sonra yapıldı.

### Görev özeti

Aday mesajının özeti:

- **Amaç:** Formdan gönderilen geçerli talebin gerçek PostgreSQL veritabanına kalıcı olarak kaydedildiği temel akışı kurmak.
- **Yapı:** .NET 10 Minimal API, `wwwroot` içinde HTML/CSS/JS, PostgreSQL ile Npgsql ve parametreli SQL. Yerelde Docker Compose ve named volume. Bağlantı ayarı `ConnectionStrings__Postgres` ortam değişkeninden okunacak. Bir web projesi ve bir test projesi yeterli.
- **Tablo:** `service_requests` (id, name, email, service, description, created_at). Kimlik ve UTC tarih sunucuda üretilecek. Kurulum sürüm kontrolündeki, tekrar çalıştırılabilir bir SQL dosyasıyla yapılacak ve veri silmeyecek.
- **API:** `POST /api/requests`.
  - Kurallar: name 2–100; email zorunlu, en fazla 254 karakter ve geçerli biçimde; service üç koddan biri; description 10–2000.
  - Eksik, null ya da yanlış tipteki alanlar kontrollü biçimde reddedilecek.
  - Yanıtlar: geçersiz istekte 400 ve alan bazında hatalar; başarılı kayıtta 201 ve requestId; veritabanı hatasında kontrollü bir sunucu hatası.
  - İstemciye SQL, parola, bağlantı dizesi ya da stack trace gönderilmeyecek. Kayıtları listeleyen bir uç nokta olmayacak.
- **Arayüz:**
  - Görünür etiketli, sade, Türkçe bir form.
  - İstemcide de aynı doğrulama kuralları.
  - Gönderim sırasında devre dışı buton ve "Gönderiliyor" yazısı.
  - Başarı mesajı yalnızca 201 ve geçerli bir requestId ile gösterilecek.
  - Hatalarda kullanıcının girdileri korunacak.
  - Otomatik tekrar olmayacak; ağ hatasında kaydın kesinlikle oluşmadığı iddia edilmeyecek.
  - Formun yanında demo uyarısı bulunacak.
- **Kapsam dışı:** Ayrıntılı tasarım, yönetim paneli, kullanıcı hesabı, e-posta gönderimi ve canlı yayın.
- **Doğrulama:** Build, otomatik doğrulama testleri ve gerçek yerel PostgreSQL ile şu kontroller: geçerli istekte 201 ve kayıt, geçersiz istekte kayıt yok, yeniden başlatmada kalıcılık.
- **Belgeler ve Git:** README, AI_LOG ve İngilizce commit mesajları. Remote oluşturulmayacak, push yapılmayacak.

### Claude'un verdiği uygulama kararları

Bunlar görevde açıkça belirtilmeyen ayrıntılardır. Aday ve Codex incelemesinde değiştirilebilirler.

- **Proje adı:** `ServiceRequests`. Ad, tabloyla ve alan adıyla uyumlu olsun diye seçildi.
- **Kimlik ve tarih:** `id` için `uuid DEFAULT gen_random_uuid()` kullanıldı; bu, onaylanan varsayımdır. Sıralı sayıdan farklı olarak kayıt sayısını dışarıya göstermez. `created_at` için `timestamptz DEFAULT now()` kullanıldı. İki değeri de veritabanı üretir; istemciden gelen `id` ya da `created_at` yok sayılır.
- **Veritabanı kısıtları:** Uzunluk ve hizmet kodu sınırları `CHECK` kısıtı olarak veritabanında da tanımlandı ve son savunma hattı olarak çalışır.
- **Şemanın uygulanması:** `DatabaseInitializer`, `schema.sql` dosyasını her açılışta çalıştırır (`CREATE TABLE IF NOT EXISTS`). Aynı yöntem ileride Neon'da da çalışır. Veritabanına ulaşılamazsa uygulama başlamaz ve çıkış kodu 1 ile kapanır. Compose'daki `restart: on-failure` ayarı uygulamayı veritabanı geri gelene kadar yeniden başlatır.
- **JSON ayrıştırma:** İstek gövdesi bir C# tipine bağlanmadan `JsonDocument` ile elle ayrıştırılır. Böylece eksik, null ya da metin dışı alanlar alan adıyla ilişkili hata mesajlarına dönüşür. Ayrıca JSON olmayan içerik türü için 415, ayrıştırılamayan ya da nesne olmayan gövde için 400 döner.
- **Karakter, kırpma ve kontrol karakterleri:**
  - Uzunluk Unicode kod noktası olarak sayılır. C# `EnumerateRunes()`, JavaScript `[...s]` ve PostgreSQL `char_length()` aynı sonucu verir; örneğin bir emoji tek karakter sayılır.
  - Kırpmada Unicode boşlukları dikkate alınır.
  - Ad ve e-postada kontrol karakterleri reddedilir. Açıklamada sekme ve satır sonu serbesttir.
  - Bu kural görevde yoktu. Gerekçesi: PostgreSQL metinde NUL karakterini saklayamaz; bu, `invalid byte sequence for encoding "UTF8": 0x00` hatasıyla doğrulandı. Kural olmasaydı doğrulamadan geçen bir girdi kayıt sırasında 500 hatasına dönüşebilirdi.
  - Geçersiz UTF-16 kaçışları (tek başına `\ud800`) "geçersiz karakter" hatası olarak döner.
- **E-posta:** RFC 5322'nin tamamı uygulanmadı; pratik bir kural seçildi. Kural boşluk, kontrol karakteri ve ikinci bir `@` içermeyen bir yerel kısım ister; alan adında en az bir nokta olmalı ve boş etiket bulunmamalıdır.
- **Hizmet kodu:** Kırpılmaz ve tam eşleşme ile denetlenir.
- **Kayıt:** INSERT açık bir transaction içinde çalışır. `requestId` yalnızca COMMIT başarılı olduktan sonra döner.
- **Hata yönetimi:**
  - Kayıt sırasında oluşan her hata uç noktada yakalanır ve sunucu loguna yazılır. İstemciye yalnızca genel mesaj ve `traceId` içeren bir 500 ProblemDetails gider.
  - Ek güvence olarak `AddProblemDetails` ve `UseExceptionHandler` de eklendi.
  - Compose içindeki konteyner `ASPNETCORE_ENVIRONMENT=Production` ile ve imajın root olmayan kullanıcısıyla çalışır.
- **Docker:**
  - Çok aşamalı bir Dockerfile kullanıldı.
  - Compose sağlık kontrolü TCP üzerinden yapılır (`pg_isready -h 127.0.0.1`). Bu önleyici bir seçimdir: PostgreSQL ilk kurulumda yalnızca soket dinleyen geçici bir sunucu çalıştırır ve uygulama bu sırada bağlanmaya çalışabilir. Bu yarış durumu gözlenmedi.
  - PostgreSQL portu dışarıya açılmadı. Uygulama portu yalnızca `127.0.0.1` adresine bağlandı.
- **Arayüz:**
  - Kurallar ayrı bir modülde (`validation.js`) tutulur; böylece Node ile test edilebilir.
  - Form `method="post"` kullanır, böylece script yüklenmezse girilen bilgiler URL'ye düşmez.
  - Başarı mesajı yalnızca 201 ve UUID biçiminde bir `requestId` ile gösterilir.
  - 400 yanıtındaki alan hataları ilgili alanlara yazılır.
  - Ağ hatasında kaydın doğrulanamadığı söylenir.
  - Beklenmeyen yanıtlarda kayıt hakkında temkinli bir mesaj gösterilir. Sunucunun kendi hata mesajı varsa o kullanılır.
  - POST otomatik olarak tekrarlanmaz.

### Karşılaşılan sorunlar ve düzeltmeler

- **Konteyner logunda native kütüphane hatası.** İlk Docker çalıştırmasında logda `Cannot load library libgssapi_krb5.so.2` hatası görüldü.
  - Nedeni: Npgsql, GSS (Kerberos) şifrelemesini denemek için bu kütüphaneyi arıyor; ASP.NET Core imajında ise bu kütüphane yok.
  - Düzeltme: Projede Kerberos kullanılmadığı için `Program.cs` içinde `GssEncryptionMode.Disable` ayarlandı.
  - Doğrulama: İmaj yeniden derlendi ve hatanın logda artık görünmediği kontrol edildi.
- **Veritabanı yokken başlatılan süreç kapanmıyordu.** Claude, belgelere "veritabanına ulaşılamazsa uygulama başlamaz" yazdıktan sonra bu ifadeyi sınadı: veritabanı durdurulmuşken uygulama yeniden başlatıldı.
  - Gözlem: Logda `Hosting failed to start` ve `Unhandled exception` görüldü. Buna rağmen konteyner "running" durumunda kaldı ve `dotnet` süreci yaklaşık %99 CPU kullanarak istek yanıtlamadan çalışmaya devam etti.
  - Deney: Aynı imaj `docker run --init` ile, yani PID 1 olarak bir init süreciyle çalıştırıldığında süreç 134 koduyla çıktı ve konteyner durdu. `--init` olmadan, yani `dotnet` PID 1 iken, süreç 25 saniye sonra hâlâ %99 CPU'daydı. Bu deney sorunun `dotnet` sürecinin PID 1 olmasıyla ilişkili olduğunu gösteriyor. Kesin kök neden ayrıca araştırılmadı.
  - Düzeltme: `Program.cs` içinde `app.Run()` çağrısı `try/catch` içine alındı. Başlangıç hatasında süreç istisnayı dışarı sızdırmadan `1` koduyla çıkıyor; hatanın ayrıntısı host tarafından zaten loglanıyor. `compose.yaml` içinde uygulamaya `restart: on-failure` eklendi.
  - Doğrulama:
    - Veritabanı kapalıyken 15 saniyede 3 yeniden başlatma oldu ve her seferinde `Startup failed; exiting with code 1.` yazıldı. CPU %0'dı, `Unhandled exception` görülmedi.
    - Veritabanı açıldığında uygulama kendiliğinden kalktı: `GET /` 200, `POST` 201 döndü.
    - `dotnet test` (68/68) ve `node --test` (46/46) yeniden çalıştırıldı.

### Yapılan kontroller

#### Otomatik testler

| Komut | Sonuç |
| --- | --- |
| `dotnet build ServiceRequests.slnx` | Başarılı; 0 uyarı, 0 hata |
| `dotnet test ServiceRequests.slnx` | 68/68 başarılı: `ServiceRequestValidatorTests` 58, `ServiceRequestEndpointTests` 10 |
| `node --test tests/client/validation.test.mjs` | 46/46 başarılı |

- Endpoint testleri gerçek HTTP hattını çalıştırır; PostgreSQL yerine bellek içi sahte bir depo kullanır.
- Hata testinde sahte depo, mesajında SQL ve parola bulunan bir `NpgsqlException` fırlatır. Test, bu bilgilerin yanıtta yer almadığını denetler.

#### Komut satırıyla yapılan entegrasyon kontrolleri

Bu kontroller Claude tarafından curl ve psql ile yapıldı. Ortam: Docker Compose, gerçek yerel PostgreSQL 17.11, `ASPNETCORE_ENVIRONMENT=Production`. Tüm test verileri kurgusaldır ve canlı Neon veritabanı kullanılmadı.

| Kontrol | Sonuç |
| --- | --- |
| Geçerli istek (boşluklu ad ve e-posta ile) | `201` ve `requestId` döndü. psql ile bu kimliğe ait kayıt bulundu; değerler kırpılmış, `created_at` UTC (`+00`). |
| Geçersiz istek (dört alanın dördü de hatalı) | `400` ve dört alan hatası döndü. Kayıt sayısı değişmedi. |
| Eksik, null ve yanlış tipteki alanlar | `400`; "zorunludur" ve "metin olmalıdır" hataları döndü. |
| Bozuk JSON ve JSON dizisi | `400` |
| `text/plain` gövde | `415` |
| `GET /api/requests` | `405`; listeleme uç noktası yok. |
| `docker compose restart app` | Kayıt aynı kimlikle duruyor. |
| `docker compose down` ve ardından `up -d --wait` (konteynerler silinip yeniden oluşturuldu, volume korundu) | Kayıt aynı kimlikle duruyor. |
| `schema.sql` dosyasının psql ile elle yeniden uygulanması | Sonuç `relation "service_requests" already exists, skipping`; satır sayısı değişmedi. |
| Veritabanı durdurulmuşken POST | `500`. Gövdede yalnızca genel mesaj ve `traceId` var. Hata ayrıntısı (`Name or service not known`, `57P01`) yalnızca sunucu logunda. Kayıt sayısı değişmedi. |
| Veritabanı yeniden başlatıldıktan sonra POST | `201` döndü; uygulama yeniden bağlandı. |
| Veritabanı kapalıyken uygulamanın başlatılması (düzeltmeden sonra) | Süreç `1` koduyla çıkıyor ve Docker onu yeniden başlatıyor. Veritabanı açılınca uygulama kalktı. Ayrıntılar yukarıdaki "Karşılaşılan sorunlar" bölümünde. |
| README'deki komutların yazıldığı hâliyle yeniden çalıştırılması | Hepsi beklenen sonucu verdi. |

#### Tarayıcıda yapılan arayüz kontrolleri

Bu kontroller insan testi değildir. Claude, Claude masaüstü uygulamasının yerleşik tarayıcısında `http://localhost:8080` adresini açtı. Sayfa durumunu okumak ve geçici simülasyonlar için sayfada JavaScript çalıştırdı.

| Kontrol | Sonuç |
| --- | --- |
| Boş formun gönderilmesi | Dört alan hatası görüntülendi, alanlar `aria-invalid` ile işaretlendi, odak ilk alana geçti ve "Lütfen işaretli alanları düzeltin." uyarısı göründü. Alan düzeltilirken hata kayboldu. |
| Geçerli gönderim (`fetch` 1,5 saniye geciktirilerek) | Gönderim sırasında buton devre dışı kaldı, "Gönderiliyor…" yazısı göründü ve `aria-busy` değeri `true` oldu. İkinci gönderim denemesi yeni bir istek oluşturmadı (yalnızca 1 `fetch`). Sonunda talep numarasıyla başarı mesajı göründü ve form temizlendi. Kayıt psql ile bulundu. |
| Simüle edilmiş yanıtlar (`fetch` geçici olarak değiştirildi) | `400` alan hataları ilgili alanlara yazıldı. Geçersiz ya da boş `requestId` ile gelen `201` yanıtında başarı gösterilmedi. HTML gövdeli `502` yanıtında temkinli mesaj gösterildi. Tüm durumlarda girdiler korundu. |
| Gerçek ağ hatası (uygulama konteyneri durduruldu) | "Talebinizin kaydedilip kaydedilmediğini doğrulayamıyoruz" mesajı göründü, girdiler korundu. |
| Gerçek veritabanı hatası (veritabanı konteyneri durduruldu) | Sunucunun genel `500` mesajı göründü, girdiler korundu. |
| 375 px genişlik | Yatay taşma yok. |
| Tarayıcı konsolu | Yalnızca bilerek oluşturulan iki hata görüldü: bağlantı reddi ve `500`. JavaScript hatası yok. |

#### Yapılamayan veya doğrulanamayan kontroller

- Adayın uygulamayı kullanarak yapacağı manuel test henüz yapılmadı.
- Codex'in kod ve test incelemesi henüz yapılmadı.
- Gerçek bir ekran okuyucuyla (VoiceOver vb.) test yapılmadı; yalnızca erişilebilirlik ağacı okundu.
- Yalnızca masaüstü uygulamasının yerleşik tarayıcısı denendi; Safari ve Firefox denenmedi.
- Gerçek PostgreSQL'e bağlanan otomatik test yok; bu kontroller komut satırından yapıldı.
- Render ve Neon üzerinde çalıştırma denenmedi; bu aşamanın kapsamı dışında.
- Docker dışında `dotnet run` ile çalıştırma denenmedi.

### Önerilerin kabulü, değiştirilmesi ve reddi

Aday ve Codex incelemesi henüz yapılmadı. Hangi önerilerin kabul edildiği, değiştirildiği veya reddedildiği inceleme sonrasında bu bölüme eklenecek.

### Commit'ler (saat +03)

| Commit | Saat | İçerik |
| --- | --- | --- |
| `15d44bf` | 13:02 | .NET 10 çözümü, web ve test projeleri, `.gitignore` |
| `de955c1` | 13:12 | `POST /api/requests`, PostgreSQL erişimi, şema, Docker Compose |
| `9d993e0` | 13:14 | Doğrulama ve API testleri |
| `8b70bdb` | 13:22 | Form ve tarayıcı tarafı doğrulama, istemci testleri |
| `383b8a7` | 13:31 | Başlangıç hatasında 1 koduyla çıkış ve compose `restart: on-failure` |
| Bu dosyanın eklendiği commit | — | README ve AI_LOG |
