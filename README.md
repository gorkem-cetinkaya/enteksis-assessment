# Otomasyon talep formu

Enteksis uygulama çalışması. Uygulama, müşteri taleplerini e-posta ile Excel arasında elle taşıyan küçük işletmelere otomasyon hizmeti sunan **kurgusal** bir şirketin tanıtım ve talep toplama uygulamasıdır.

| Hizmet kodu | Hizmet |
| --- | --- |
| `workflow-automation` | İş akışı otomasyonu |
| `api-integration` | API ve veri entegrasyonu |
| `ai-triage` | AI destekli talep sınıflandırma |

Hizmetler yalnızca tanıtılır. Uygulamada gerçek LLM çağrısı ya da otomasyon motoru yoktur.

> **Durum: 1. aşama.** Formdan gönderilen geçerli talep gerçek bir PostgreSQL veritabanına kalıcı olarak kaydediliyor. Tanıtım sayfasının içeriği ve canlı yayın sonraki aşamalarda yapılacak. Ayrıntılar için [bilinen eksikler](#bilinen-eksikler-ve-sonraki-aşamalar) bölümüne bakın.

## Teknolojiler

- .NET 10 ve ASP.NET Core Minimal API
- HTML, CSS ve JavaScript; derleme adımı yoktur, dosyalar aynı uygulamanın `wwwroot` klasöründen sunulur
- PostgreSQL 17 ve Npgsql 10; veri erişimi parametreli SQL ile yapılır
- Yerelde Docker Compose
- Testler için xUnit ve `node:test`

## Yerelde çalıştırma

Docker Desktop gerekir (Compose v2 veya üstü). Denenen sürümler: Docker Engine 29.8.2 ve Compose v5.5.1.

```bash
docker compose up --build -d
```

Uygulama adresi: <http://localhost:8080>

Durum ve loglar:

```bash
docker compose ps
docker compose logs app
```

Durdurma:

```bash
docker compose down
```

Veriler `postgres-data` adlı named volume'de saklanır ve `docker compose down` komutundan sonra korunur. `docker compose down -v` ise volume'ü ve tüm kayıtları siler; bu komut yalnızca verileri gerçekten silmek istediğinizde kullanılmalıdır.

### Yapılandırma

- Uygulama veritabanı bağlantısını yalnızca `ConnectionStrings__Postgres` ortam değişkeninden okur. Değer Npgsql biçiminde olmalıdır: `Host=...;Port=5432;Database=...;Username=...;Password=...`. Değişken tanımlı değilse uygulama açılmaz.
- `compose.yaml` bu değeri yalnızca yerel geliştirmeye ait varsayılan değerlerle oluşturur. Bu değerleri değiştirmek için `.env.example` dosyasını `.env` adıyla kopyalayıp düzenleyin. `.env` dosyası Git'e eklenmez.
- PostgreSQL portu bilgisayara açılmaz. Uygulamaya yalnızca `127.0.0.1:8080` üzerinden erişilebilir.

## Otomatik testler

Testler için .NET 10 SDK gerekir (denenen sürüm: 10.0.400). Tarayıcı kurallarının testleri için ayrıca Node.js 20.19 veya üstü gerekir (denenen sürüm: v20.20.0).

```bash
dotnet test ServiceRequests.slnx
node --test tests/client/validation.test.mjs
```

| Test | Sayı | Kapsam |
| --- | --- | --- |
| `ServiceRequestValidatorTests` | 58 | Sunucu tarafı doğrulama kuralları |
| `ServiceRequestEndpointTests` | 10 | Gerçek HTTP hattı, PostgreSQL yerine bellek içi sahte depo ile: 201, 400, 415, kontrollü 500, listeleme uç noktasının olmaması ve ana sayfa |
| `tests/client/validation.test.mjs` | 46 | Tarayıcı tarafı kurallar; .NET testleriyle aynı durumlar ve aynı mesajlar |

Bu testler veritabanına bağlanmaz. Gerçek PostgreSQL ile yapılan kontroller aşağıdadır.

## Gerçek PostgreSQL ile komut satırı kontrolleri

Aşağıdaki komutlar uygulama `docker compose up --build -d` ile çalışırken kullanıldı. Sonuçlar [AI_LOG.md](AI_LOG.md) dosyasında yer alıyor.

Geçerli istek. Beklenen yanıt `201` ve `{"requestId":"..."}`:

```bash
curl -i -X POST http://localhost:8080/api/requests -H 'Content-Type: application/json' -d '{"name":"Deniz Örnek","email":"deniz.ornek@example.com","service":"api-integration","description":"Kurgusal test: kalıcılık kontrolü."}'
```

Dönen `requestId` ile kaydı sorgulama:

```bash
docker compose exec -T db psql -U enteksis -d enteksis -c "SELECT id, name, service, created_at FROM service_requests WHERE id = '<requestId>';"
```

Geçersiz istek. Beklenen yanıt `400` ve alan bazında hatalar; kayıt oluşmaz:

```bash
curl -i -X POST http://localhost:8080/api/requests -H 'Content-Type: application/json' -d '{"name":"D","email":"deniz@example","service":"consulting","description":"kısa"}'
docker compose exec -T db psql -U enteksis -d enteksis -c "SELECT count(*) FROM service_requests;"
```

Yeniden başlatmadan sonra kaydın korunduğunu görmek için aşağıdaki komutları çalıştırın, ardından kaydı yukarıdaki sorguyla yeniden arayın:

```bash
docker compose restart app
docker compose down
docker compose up -d --wait
```

Şemayı yeniden uygulama. Komut tablo zaten varsa bunu bildirir ve veriye dokunmaz:

```bash
docker compose exec -T db psql -v ON_ERROR_STOP=1 -U enteksis -d enteksis < src/ServiceRequests.Web/Data/schema.sql
```

## Yapı

```text
ServiceRequests.slnx
compose.yaml, Dockerfile, .env.example
src/ServiceRequests.Web/
  Program.cs                           servisler ve istek hattı
  Requests/ServiceRequestValidator.cs  sunucu doğrulama kuralları (son karar burada)
  Requests/ServiceRequestEndpoints.cs  POST /api/requests
  Data/schema.sql                      tekrar çalıştırılabilir tablo kurulumu
  Data/DatabaseInitializer.cs          schema.sql dosyasını açılışta uygular
  Data/PostgresServiceRequestStore.cs  parametreli INSERT ve COMMIT
  wwwroot/index.html, styles.css       form
  wwwroot/validation.js                tarayıcı tarafı kurallar
  wwwroot/app.js                       gönderim ve durum mesajları
tests/ServiceRequests.Web.Tests/       xUnit testleri
tests/client/validation.test.mjs       tarayıcı kurallarının testleri
```

## İstekten veritabanına akış

1. **Tarayıcı:** `validation.js`, alanları sunucuyla aynı kurallarla denetler. Hata varsa istek gönderilmez, hatalar alanların altında gösterilir ve odak ilk hatalı alana taşınır. Hata yoksa buton devre dışı kalır, "Gönderiliyor…" yazısı görünür ve `fetch` tek bir JSON POST isteği gönderir. İstek otomatik olarak tekrarlanmaz.
2. **API (`POST /api/requests`):** Sırasıyla şunlar denetlenir:
   - İçerik türü JSON değilse `415` döner.
   - Gövde ayrıştırılamıyorsa `400` döner.
   - Gövde bir JSON nesnesi değilse `400` döner.
   - `ServiceRequestValidator` alanları denetler; geçersiz alan varsa `400` ve alan bazında hatalar döner.
3. **Veritabanı:** `PostgresServiceRequestStore`, bir transaction içinde parametreli `INSERT … RETURNING id` çalıştırır. `COMMIT` başarılı olduktan sonra API `201 {"requestId": "<uuid>"}` döner. `id` ve `created_at` değerlerini veritabanı üretir.
4. **Hata durumu:** Veritabanı hatası sunucu loguna yazılır. İstemciye yalnızca genel bir mesaj içeren `500` gönderilir; SQL, bağlantı dizesi ya da stack trace gönderilmez.
5. **Arayüz:**
   - Başarı mesajı yalnızca `201` ve geçerli bir UUID `requestId` geldiğinde gösterilir; ardından form temizlenir.
   - `400` yanıtındaki alan hataları ilgili alanlara yazılır.
   - Ağ hatasında kaydın oluşup oluşmadığının doğrulanamadığı söylenir.
   - Başarı dışındaki tüm durumlarda girilen bilgiler formda kalır.

## API

`POST /api/requests`, `Content-Type: application/json`

```json
{
  "name": "Deniz Örnek",
  "email": "deniz.ornek@example.com",
  "service": "ai-triage",
  "description": "Gelen talepleri otomatik sınıflandırmak istiyoruz."
}
```

| Durum | Yanıt |
| --- | --- |
| `201` | `{"requestId": "<uuid>"}`; kayıt commit edildikten sonra döner |
| `400` | Doğrulama hatası: alan adlarıyla eşleşen `errors` içeren ProblemDetails |
| `400` | Gövde geçerli bir JSON değil ya da JSON nesnesi değil |
| `415` | İçerik türü JSON değil |
| `500` | Kayıt yapılamadı; yalnızca genel mesaj döner |

Kayıtları listeleyen ya da tek tek okuyan bir uç nokta yoktur. `GET /api/requests` isteği `405` döner.

## Doğrulama kuralları

Kurallar istemcide (`wwwroot/validation.js`) ve sunucuda (`ServiceRequestValidator.cs`) aynıdır. Sunucu doğrulaması her istekte çalışır.

| Alan | Kural |
| --- | --- |
| `name` | Baştaki ve sondaki boşluklar kırpılır. 2–100 karakter olmalı ve kontrol karakteri içermemelidir. |
| `email` | Boşluklar kırpılır. Zorunludur ve en fazla 254 karakter olabilir. `yerel@alan.adı` biçiminde olmalıdır: boşluk, kontrol karakteri ya da ikinci bir `@` içeremez; alan adında en az bir nokta bulunmalı ve boş etiket (`a..b`) olmamalıdır. |
| `service` | Yalnızca yukarıdaki üç koddan biri kabul edilir. Kırpma yapılmaz, büyük/küçük harf duyarlıdır. |
| `description` | Baştaki ve sondaki boşluklar kırpılır. 10–2000 karakter olmalıdır. Sekme ve satır sonu serbesttir, diğer kontrol karakterleri reddedilir. |
| Tüm alanlar | Eksik ya da `null` alan için "zorunludur", metin dışı tip için "metin olmalıdır" hatası döner. `id` veya `created_at` gibi bilinmeyen alanlar yok sayılır. |

- "Karakter", Unicode kod noktası anlamına gelir; örneğin bir emoji tek karakter sayılır. Bu sayım C#'ta `EnumerateRunes()`, JavaScript'te `[...metin].length`, PostgreSQL'de `char_length()` ile aynı sonucu verir.
- Kırpma Unicode boşluk karakterlerine uygulanır.
- Kontrol karakterleri reddedilir, çünkü örneğin PostgreSQL metin alanında NUL (U+0000) karakterini saklayamaz. Bu kural olmasaydı doğrulamadan geçen bir girdi kayıt sırasında `500` hatasına dönüşebilirdi.

## Veritabanı

`service_requests` tablosu `src/ServiceRequests.Web/Data/schema.sql` dosyasında tanımlıdır:

| Sütun | Tip |
| --- | --- |
| `id` | `uuid`, varsayılan `gen_random_uuid()` |
| `name`, `email`, `service`, `description` | `text`; uzunluk ve hizmet kodu için `CHECK` kısıtları |
| `created_at` | `timestamptz`, varsayılan `now()`; zaman UTC anı olarak saklanır |

Betik `CREATE TABLE IF NOT EXISTS` kullanır ve hiçbir veriyi silmez. Uygulama her açılışta bu betiği çalıştırır. Veritabanına ulaşılamazsa uygulama başlamaz ve çıkış kodu 1 ile kapanır. `compose.yaml` içindeki `restart: on-failure` ayarı, veritabanı yeniden erişilebilir olana kadar uygulamayı tekrar başlatır.

## Bilinen eksikler ve sonraki aşamalar

- Tanıtım sayfasının içeriği (hizmetlerin anlatımı) ve görsel tasarım henüz yok; bu aşamada yalnızca sade bir form bulunuyor.
- Canlı yayın yapılmadı. Render ve Neon'a bağlanılmadı. Canlı ortamda Neon bağlantı dizesi Npgsql biçimine çevrilip `ConnectionStrings__Postgres` olarak verilecek; Render tarafındaki port ayarı da o aşamada kontrol edilecek. Konteyner 8080 portunu dinliyor.
- Hız sınırı, uygulamaya özel istek gövdesi boyut sınırı ve güvenlik başlıkları (CSP vb.) henüz yok. Şu an Kestrel'in varsayılan gövde sınırı (yaklaşık 30 MB) geçerli.
- İdempotency anahtarı yok. Yanıtı kaybolan bir isteği kullanıcı elle yeniden gönderirse aynı talep iki kez kaydedilebilir. Arayüz bu durumu ağ hatası mesajında belirtiyor.
- Gerçek PostgreSQL'e bağlanan otomatik entegrasyon testi yok; kalıcılık komut satırından kontrol edildi.
- JavaScript kapalıyken form gönderilemez. Bu durumda `noscript` uyarısı gösterilir; `method="post"` sayesinde girilen bilgiler URL'ye yazılmaz.
- Docker dışında `dotnet run` ile çalıştırma belgelenmedi ve denenmedi.
- Konteynerde `dotnet` süreci PID 1 olarak çalışıyor. Başlangıçtaki hata artık düzgün bir çıkışla sonuçlanıyor. Ancak başlangıç dışında beklenmeyen bir çökme olursa süreç yine takılı kalabilir. İmaja `tini` gibi bir init süreci eklenmesi canlı yayın aşamasında değerlendirilecek.

## AI kullanımı

Kullanılan araçlar, verilen kararlar ve yapılan kontroller [AI_LOG.md](AI_LOG.md) dosyasında yer alıyor.
