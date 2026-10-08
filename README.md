# Örnek Otomasyon: tanıtım sayfası ve talep formu

Enteksis uygulama çalışması. Uygulama, müşteri taleplerini e-posta ile Excel arasında elle taşıyan küçük işletmelere otomasyon hizmeti sunan **kurgusal** bir şirketin ("Örnek Otomasyon") tanıtım ve talep toplama uygulamasıdır.

| Hizmet kodu | Hizmet |
| --- | --- |
| `workflow-automation` | İş akışı otomasyonu |
| `api-integration` | API ve veri entegrasyonu |
| `ai-triage` | AI destekli talep sınıflandırma |

Hizmetler yalnızca tanıtılır. Uygulamada gerçek LLM çağrısı ya da otomasyon motoru yoktur.

> **Durum: 3. aşama.** Tanıtım sayfası ve talep formu yerelde çalışıyor; geçerli talepler gerçek bir PostgreSQL veritabanına kalıcı olarak kaydediliyor. Bu aşamada şunlar eklendi: API sınırları, tarayıcı güvenlik başlıkları, gerçek PostgreSQL ile otomatik testler, `/health` uç noktası ve yayın hazırlığı. Canlı yayın (Render + Neon) henüz yapılmadı. Ayrıntılar için [bilinen eksikler](#bilinen-eksikler-ve-sonraki-aşamalar) bölümüne bakın.

## Sayfa

Türkçe, mobil uyumlu tek bir sayfadır (`wwwroot/index.html`). Bölümler sırasıyla şunlardır:

1. **Giriş:** Hizmetin kime ve hangi konuda yardımcı olduğunu anlatır ve forma giden "Talep oluştur" bağlantısını içerir.
2. **Hizmetler:** Üç kart vardır; kart başlıkları formdaki hizmet seçenekleriyle birebir aynıdır. Bu eşleşme bir testle denetlenir.
3. **Kurgusal örnek:** Bir klima servis işletmesinde bugün elle yapılan işi ve önerilen otomasyon akışını karşılaştırır. Her adımda ilgili hizmet etiketle gösterilir.
4. **Süreç:** Üç adımdan oluşur: ihtiyacı anlatma, akışı birlikte planlama, küçük ölçekte deneyip doğrulama.
5. **Talep formu:** Mevcut çalışan form ve hemen yanında kurgusal bilgi kullanılması gerektiğini söyleyen demo uyarısı.

Sayfada gerçek müşteri, referans, başarı oranı ya da ölçülmüş kazanç yoktur. Örnek bölümü kurgusal olduğunu ve ölçüm yapılmadığını açıkça söyler; altbilgi de şirketin kurgusal olduğunu ve demoda yapay zekâ ya da otomasyon çalıştırılmadığını belirtir.

Erişilebilirlik:

- Sayfada tek bir `h1`, her bölüm için bir `h2`, kartlar ve adımlar için `h3` vardır.
- "İçeriğe geç" atlama bağlantısı ve görünür klavye odağı (3 px çerçeve) bulunur.
- Form alanlarının görünür etiketleri vardır; hata ve ipucu metinleri alanlara `aria-describedby` ile bağlıdır.
- Durum mesajları `role="status"`, hata mesajları `role="alert"` bölgelerinde gösterilir.
- Stil verilmiş listelerde `role="list"` kullanılır.
- Renk çiftleri WCAG AA'yı karşılar: metinler en az 5,77:1, metin dışı öğeler en az 4,74:1.
- 320 px genişlikte yatay taşma yoktur.

Harici font, script ya da görsel kullanılmaz.

## Teknolojiler

- .NET 10 ve ASP.NET Core Minimal API
- HTML, CSS ve JavaScript; derleme adımı yoktur, dosyalar aynı uygulamanın `wwwroot` klasöründen sunulur
- PostgreSQL 17 ve Npgsql 10; veri erişimi parametreli SQL ile yapılır
- Yerelde Docker Compose; konteynerde PID 1 olarak `tini`
- Testler için xUnit, `node:test` ve gerçek PostgreSQL testleri için Testcontainers

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
- Statik dosyalar `Cache-Control: no-cache` başlığıyla sunulur. Tarayıcı her yüklemede dosyayı ETag ile yeniden doğrular; dosya değişmediyse `304` döner. Böylece yeni bir sürümden sonra önbellekteki eski `app.js` kullanılmaz. Bu başlık eklenmeden önce önbelleğe alınmış bir kopya varsa sayfayı bir kez zorla yenilemek (Cmd/Ctrl+Shift+R) gerekebilir.

## Güvenlik ve sınırlar

- **Hız sınırı (yalnızca `POST /api/requests`):** ASP.NET Core'un yerleşik hız sınırlayıcısı kullanılır. Sabit pencereyle 60 saniyede en fazla 20 istek kabul edilir, kuyruk yoktur. Sınır aşılınca `429`, Türkçe bir ProblemDetails ve `Retry-After` döner. Sayfa, statik dosyalar ve `/health` bu sınırdan etkilenmez.
  - Sayaç bilinçli olarak uygulama örneği başına tek ve ortaktır: tüm ziyaretçiler aynı sınırı paylaşır, uygulama yeniden başlatılınca sayaç sıfırlanır, birden fazla örnek çalışırsa her birinin kendi sayacı olur.
  - Bu, kapsamlı bir DDoS koruması değildir.
  - İstemci IP'si kullanılmaz; bu yüzden sahte bir `X-Forwarded-For` başlığı sonucu değiştiremez.
- **İstek gövdesi:** En fazla 32 KiB kabul edilir.
  - `Content-Length` bu sınırdan büyükse istek okunmadan `413` döner.
  - Uzunluğu önceden bildirilmeyen (chunked) gövdelerde, sınırı bir bayt aşan veri okunduğu anda `413` döner.
  - Bu durum hiçbir zaman `500`'e dönüşmez.
- **Tarayıcı güvenlik başlıkları (hata yanıtları dahil her yanıtta):**
  - `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'`
  - `X-Content-Type-Options: nosniff`
  - `Referrer-Policy: no-referrer`
  - Sayfa satır içi script ya da stil kullanmadığı için `unsafe-inline` gerekmez.
  - HTTPS yönlendirmesi ya da proxy güven ayarı eklenmedi; Render'daki HTTPS davranışı yayın aşamasında kontrol edilecek.
- **Arayüz:**
  - `429` ve `413` yanıtlarında talebin kaydedilmediği ve ne yapılması gerektiği anlatılır.
  - Tarayıcı 30 saniye içinde yanıt alamazsa beklemeyi bırakır ve kaydın yapılıp yapılmadığının bilinmediğini söyler. Tarayıcının beklemeyi bırakması, sunucudaki işlemin iptal edildiği anlamına gelmez.
  - Her durumda alanlar yeniden açılır ve girilen bilgiler formda kalır. İstek otomatik olarak yeniden gönderilmez.
- **`GET /health`:** Yalnızca `{"status":"ok","commit":...}` döner.
  - `commit` değeri Render'ın verdiği `RENDER_GIT_COMMIT` değişkeninden okunur; değişken yoksa (ör. yerelde) `null` olur.
  - Bu uç nokta **veritabanını kontrol etmez**; yalnızca uygulamanın yanıt verdiğini ve hangi commit'in çalıştığını gösterir.
- **Konteyner:** `tini` PID 1 olarak çalışır, `dotnet` süreci ise root olmayan `app` kullanıcısıyla (UID 1654) çalışır.

## Otomatik testler

Testler için .NET 10 SDK gerekir (denenen sürüm: 10.0.400). Tarayıcı kurallarının testleri için ayrıca Node.js 20.19 veya üstü gerekir (denenen sürüm: v20.20.0).

Birim ve API testleri (Docker gerekmez):

```bash
dotnet test tests/ServiceRequests.Web.Tests
```

Gerçek PostgreSQL testleri (**Docker çalışıyor olmalı**):

```bash
dotnet test tests/ServiceRequests.Web.IntegrationTests
```

Hepsi birlikte (Docker gerekir):

```bash
dotnet test ServiceRequests.slnx
```

Tarayıcı kuralları:

```bash
node --test tests/client/validation.test.mjs
```

| Test | Sayı | Kapsam |
| --- | --- | --- |
| `ServiceRequestValidatorTests` | 85 | Sunucu tarafı doğrulama kuralları |
| `ServiceRequestEndpointTests` | 22 | PostgreSQL yerine bellek içi sahte depo ile HTTP hattı: 201, 400 (e-posta kuralları dahil), 415, kontrollü 500, listeleme uç noktasının olmaması, sayfa bölümleri, kartlarla form seçeneklerinin ve hizmet kodlarının eşleşmesi, statik dosyalarda `no-cache` |
| `RequestLimitTests` | 5 | 21. istekte `429` ve `Retry-After`; statik dosyaların sınırlanmaması; tam 32 KiB'ın kabul edilmesi; `Content-Length` ile ve olmadan `413` |
| `SecurityHeaderTests` | 7 | Güvenlik başlıklarının 200, 201, 400, 404, 413, 429 ve 500 yanıtlarında bulunması |
| `HealthEndpointTests` | 3 | `/health` içeriği, `RENDER_GIT_COMMIT` değeri, hız sınırından etkilenmemesi |
| `PostgresPersistenceTests` | 3 | Gerçek PostgreSQL 17 (Testcontainers): `201` ve kaydın başka bir bağlantıdan kırpılmış olarak okunması; geçersiz istekte kayıt oluşmaması; yeni bir uygulama host'unda önceki kaydın korunması |
| `DatabaseOutageTests` | 1 | Çalışan uygulamanın veritabanı durdurulunca `requestId` ve ayrıntı içermeyen genel bir `500` dönmesi |
| `tests/client/validation.test.mjs` | 75 | Tarayıcı tarafı kurallar; .NET testleriyle aynı durumlar ve aynı mesajlar |

Gerçek PostgreSQL testleri her çalıştırmada kendi geçici `postgres:17-alpine` konteynerini başlatır ve iş bitince kaldırır. Compose veritabanına, onun volume'üne ya da Neon'a dokunmaz. Docker yoksa bu testler atlanmaz, **başarısız olur**: Docker soketi olmayan bir konteynerde 4 testin 4'ü de başarısız oldu ve `dotnet test` 1 koduyla çıktı.

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
  SecurityHeaders.cs                   CSP, nosniff, Referrer-Policy
  HealthEndpoint.cs                    GET /health
  Requests/ServiceRequestValidator.cs  sunucu doğrulama kuralları (son karar burada)
  Requests/ServiceRequestEndpoints.cs  POST /api/requests
  Requests/RequestLimits.cs            hız sınırı ve 32 KiB gövde sınırı
  Data/schema.sql                      tekrar çalıştırılabilir tablo kurulumu
  Data/DatabaseInitializer.cs          schema.sql dosyasını açılışta uygular
  Data/PostgresServiceRequestStore.cs  parametreli INSERT ve COMMIT
  wwwroot/index.html, styles.css       tanıtım sayfası ve form
  wwwroot/validation.js                tarayıcı tarafı kurallar
  wwwroot/app.js                       gönderim ve durum mesajları
tests/ServiceRequests.Web.Tests/              birim ve API testleri (sahte depo)
tests/ServiceRequests.Web.IntegrationTests/   gerçek PostgreSQL testleri (Testcontainers)
tests/client/validation.test.mjs              tarayıcı kurallarının testleri
```

## İstekten veritabanına akış

1. **Tarayıcı:** `validation.js`, alanları sunucuyla aynı kurallarla denetler. Hata varsa istek gönderilmez, hatalar alanların altında gösterilir ve odak ilk hatalı alana taşınır. Hata yoksa dört alan ve buton devre dışı kalır, "Gönderiliyor…" yazısı görünür ve `fetch` tek bir JSON POST isteği gönderir. Böylece beklerken yazılan ve isteğe girmeyecek değişiklikler oluşmaz. Sonuç ne olursa olsun alanlar yeniden açılır. İstek otomatik olarak tekrarlanmaz.
2. **API (`POST /api/requests`):** Sırasıyla şunlar denetlenir:
   - Hız sınırı aşıldıysa `429` döner.
   - İçerik türü JSON değilse `415` döner.
   - Gövde 32 KiB'tan büyükse `413` döner.
   - Gövde ayrıştırılamıyorsa `400` döner.
   - Gövde bir JSON nesnesi değilse `400` döner.
   - `ServiceRequestValidator` alanları denetler; geçersiz alan varsa `400` ve alan bazında hatalar döner.
3. **Veritabanı:** `PostgresServiceRequestStore`, bir transaction içinde parametreli `INSERT … RETURNING id` çalıştırır. `COMMIT` başarılı olduktan sonra API `201 {"requestId": "<uuid>"}` döner. `id` ve `created_at` değerlerini veritabanı üretir.
4. **Hata durumu:** Veritabanı hatası sunucu loguna yazılır. İstemciye yalnızca genel bir mesaj içeren `500` gönderilir; SQL, bağlantı dizesi ya da stack trace gönderilmez. Mesaj, kaydın oluşmadığını iddia etmez; yalnızca kaydın doğrulanamadığını söyler. Bunun nedeni, bağlantının COMMIT'ten hemen sonra kopması durumunda kaydın yine de oluşmuş olabilmesidir. Bu senaryo denenmedi; ifade kod yolundaki olası sonuçlara dayanıyor.
5. **Arayüz:**
   - Başarı mesajı yalnızca `201` ve geçerli bir UUID `requestId` geldiğinde gösterilir; ardından form temizlenir.
   - `400` yanıtındaki alan hataları ilgili alanlara yazılır.
   - Ağ hatasında ve 30 saniyelik zaman aşımında kaydın oluşup oluşmadığının bilinmediği söylenir.
   - `429` ve `413` yanıtlarında talebin kaydedilmediği ve ne yapılması gerektiği anlatılır.
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
| `413` | Gövde 32 KiB'tan büyük |
| `415` | İçerik türü JSON değil |
| `429` | Hız sınırı aşıldı; `Retry-After` başlığıyla birlikte döner |
| `500` | Kayıt doğrulanamadı: "Talebinizin kaydedildiğini doğrulayamadık…"; yalnızca genel mesaj döner |

`GET /health` isteği `{"status":"ok","commit":"<RENDER_GIT_COMMIT ya da null>"}` döner ve veritabanını kontrol etmez.

Kayıtları listeleyen ya da tek tek okuyan bir uç nokta yoktur. `GET /api/requests` isteği `405` döner.

## Doğrulama kuralları

Kurallar istemcide (`wwwroot/validation.js`) ve sunucuda (`ServiceRequestValidator.cs`) aynıdır. Sunucu doğrulaması her istekte çalışır.

| Alan | Kural |
| --- | --- |
| `name` | Baştaki ve sondaki boşluklar kırpılır. 2–100 karakter olmalı ve kontrol karakteri içermemelidir. |
| `email` | Boşluklar kırpılır. Zorunludur ve toplamda en fazla 254 karakter olabilir. Ayrıntılar aşağıdaki "E-posta kuralı" bölümünde. |
| `service` | Yalnızca yukarıdaki üç koddan biri kabul edilir. Kırpma yapılmaz, büyük/küçük harf duyarlıdır. |
| `description` | Baştaki ve sondaki boşluklar kırpılır. 10–2000 karakter olmalıdır. Sekme ve satır sonu serbesttir, diğer kontrol karakterleri reddedilir. |
| Tüm alanlar | Eksik ya da `null` alan için "zorunludur", metin dışı tip için "metin olmalıdır" hatası döner. `id` veya `created_at` gibi bilinmeyen alanlar yok sayılır. |

### E-posta kuralı

Adres `yerel-kısım@alan.adı` biçiminde olmalı ve tam olarak bir `@` içermelidir.

- **`@` öncesi (yerel kısım):**
  - En fazla 64 karakter olabilir.
  - ASCII "dot-atom" kuralı uygulanır: noktayla ayrılmış, boş olmayan parçalar. Parçalarda harf, rakam ve ``! # $ % & ' * + - / = ? ^ _ ` { | } ~`` işaretleri kullanılabilir.
  - Başta, sonda ya da art arda gelen nokta, boşluk, kontrol karakteri, `<` ve `>` reddedilir.
  - Tırnaklı (`"ad soyad"@…`) ve ASCII dışı karakter içeren yerel kısımlar bu demonun kapsamı dışındadır, bu yüzden reddedilir.
- **`@` sonrası (alan adı):**
  - Noktayla ayrılmış en az iki etiketten oluşur.
  - Her etiket herhangi bir alfabeden bir harf ya da ondalık rakamla başlar ve biter; arada tire bulunabilir. Bu nedenle `/`, `<`, `>`, `_` ve boş etiketler (`a..b`, `.a`, `a.`) reddedilir.
  - Alt alan adları (`mail.example.com.tr`), Unicode harfli alan adları (`örnek.com.tr`) ve punycode (`xn--…`) kabul edilir.
  - Denetim kod noktası düzeyinde yapılır: C#'ta `Rune.IsLetter` / `Rune.IsDigit`, JavaScript'te `u` bayraklı `\p{L}` / `\p{Nd}`. Böylece BMP dışındaki harfler de (ör. `codex@𐐀.example`) iki tarafta aynı sonucu verir.
- Bu yalnızca bir biçim kontrolüdür; adresin gerçekten var olduğunu kanıtlamaz. Tam RFC 5321/5322 ya da IDNA doğrulaması, DNS sorgusu veya e-posta gönderimi yapılmaz.
- .NET ve tarayıcının kullandığı Unicode sürümleri farklı olabilir. Bu nedenle çok yeni eklenmiş karakterlerde iki taraf farklı sonuç verebilir; böyle bir durumda sunucunun kararı geçerlidir.

### Karakter sayımı ve kırpma

- "Karakter", Unicode kod noktası anlamına gelir. Örneğin 😀 tek bir kod noktasıdır. Ekranda tek simge gibi görünen birleşik emojiler ise birden fazla sayılır; örneğin 👨‍👩‍👧, sıfır genişlikli birleştiricilerle birlikte 5 kod noktasıdır. Bu sayım C#'ta `EnumerateRunes()`, JavaScript'te `[...metin].length`, PostgreSQL'de `char_length()` ile aynı sonucu verir.
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

## Yayın hazırlığı: Render + Neon (Frankfurt)

Bu adımlar henüz uygulanmadı; yayın aşamasında izlenecek. Gerçek bağlantı bilgileri yalnızca Render'ın ortam değişkenlerine girilir; repoya ya da sohbete yazılmaz.

1. **Neon:** Neon projesi zaten mevcut; ikinci bir proje oluşturulmaz. Mevcut kaynaklar:
   - Proje `enteksis-assessment`, bölge AWS Europe Central 1 (Frankfurt)
   - Dal `production`, veritabanı `neondb`, rol `neondb_owner`
   - Connection pooling açık; bu yüzden `Host` olarak pooler adresi kullanılır.

   Bağlantı bilgisi konsolun .NET bağlantı penceresinden alınır ve Npgsql anahtar/değer biçiminde verilir. Biçim (yer tutucularla):

     ```text
     Host=<neon-pooler-host>;Port=5432;Database=neondb;Username=neondb_owner;Password=<parola>;SSL Mode=VerifyFull;Channel Binding=Require
     ```

   - Uygulama `postgresql://...` URI biçimini doğrudan ayrıştırmaz; yalnızca yukarıdaki anahtar/değer biçimi kullanılır.
   - Sertifika doğrulaması kapatılmaz.
   - Gerçek parola yerel bir dosyaya, `appsettings` dosyasına ya da repoya yazılmaz; yalnızca Render'ın gizli ortam değişkeni alanına girilir.

   - `SSL Mode=VerifyFull` ile Npgsql sunucu sertifikasını makinenin CA deposuyla doğrular ve bağlanılan sunucunun belirtilen sunucu olduğunu denetler. Npgsql'in varsayılanı `Prefer`'dır; `Prefer` ve `Require` ortadaki adam saldırısına karşı koruma sağlamaz. Bu bilgiler Npgsql güvenlik belgesine dayanıyor; Neon'un .NET rehberi de `SSL Mode=VerifyFull; Channel Binding=Require` öneriyor. Çalışma imajında CA sertifikaları (`/etc/ssl/certs/ca-certificates.crt`) bulunuyor.
   - Tablo, uygulama ilk açıldığında `schema.sql` ile oluşturulur; elle kurulum gerekmez.
2. **Render:** New → Web Service ile GitHub deposu seçilir. Ayarlar:
   - Runtime: Docker (repodaki `Dockerfile`, build context depo kökü)
   - Branch: `main`
   - Region: Frankfurt
   - Instance: Free
   - Health Check Path: `/health`
   - `ConnectionStrings__Postgres`: Neon bağlantı dizesi, gizli ortam değişkeni olarak
   - `PORT`: `8080`
   - `RENDER_GIT_COMMIT` elle girilmez; değeri Render sağlar.
3. **Port uyumu:** Render belgelerine göre web servisi `0.0.0.0` adresine bağlanmalı ve `PORT` değişkeninin varsayılanı `10000`'dir. Uygulama `ASPNETCORE_HTTP_PORTS=8080` ile tüm adreslerde 8080 portunu dinlediği için Render'da `PORT=8080` verilmelidir; iki değer aynı olmalı.
4. **Commit bilgisi:** `RENDER_GIT_COMMIT` değişkenini Render ayarlar ("the commit SHA for a service or deploy"). `/health` yanıtındaki `commit` bu değeri gösterir.
5. **Yayından sonra:**
   - `https://<servis>.onrender.com/health` adresindeki `commit` değeri teslim commit'iyle aynı olmalı.
   - Formdan kurgusal bir talep gönderilmeli; yeniden deploy'dan sonra kaydın Neon'da durduğu doğrulanmalı.
   - HTTPS davranışı kontrol edilmeli.
6. **Render Free:** Render belgelerine göre ücretsiz servis 15 dakika trafik almazsa uyur ve uyanması yaklaşık 1 dakika sürer. Dosya sistemi geçicidir; veriler Neon'da tutulduğu için bu bir sorun değildir.

## Bilinen eksikler ve sonraki aşamalar

- Canlı yayın yapılmadı; Render'a ve Neon'a bağlanılmadı. Yukarıdaki adımlar ve Render'daki HTTPS kontrolü yayın aşamasında yapılacak.
- Hız sınırı sayacı bilinçli olarak tek ve ortaktır: yeniden başlatmada sıfırlanır, birden fazla örnek arasında paylaşılmaz ve DDoS koruması değildir.
- `/health` veritabanını kontrol etmiyor.
- E-postada tırnaklı ve ASCII dışı yerel kısımlar kabul edilmiyor. Biçim kontrolü adresin var olduğunu kanıtlamıyor.
- İdempotency anahtarı yok. Yanıtı kaybolan bir isteği kullanıcı elle yeniden gönderirse aynı talep iki kez kaydedilebilir. Arayüz bu durumu ağ hatası mesajında belirtiyor.
- JavaScript kapalıyken form gönderilemez. Bu durumda `noscript` uyarısı gösterilir; `method="post"` sayesinde girilen bilgiler URL'ye yazılmaz.
- Docker dışında `dotnet run` ile çalıştırma belgelenmedi ve denenmedi.
- Sayfa gerçek bir ekran okuyucuyla, Safari'de ya da Firefox'ta denenmedi. Hizmet seçim kutusu tarayıcının yerel öğesidir ve otomasyon aracıyla klavyeden sürülemedi; yalnızca seçilmiş değerle klavye akışı denendi.
- 1. aşamada, `dotnet` PID 1 iken başlangıç hatasında süreç kapanmadan %100'e yakın CPU kullanarak takılı kalmıştı; `docker run --init` ile çalıştırıldığında ise normal şekilde çıkmıştı. Bu durum önce çıkış kodu döndürülerek giderildi, 3. aşamada imaja `tini` eklendi. Eski takılmanın kök nedeni doğrulanmadı.

## AI kullanımı

Kullanılan araçlar, verilen kararlar ve yapılan kontroller [AI_LOG.md](AI_LOG.md) dosyasında yer alıyor.
