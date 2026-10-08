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
  - Uzunluk Unicode kod noktası olarak sayılır. C# `EnumerateRunes()`, JavaScript `[...s]` ve PostgreSQL `char_length()` aynı sonucu verir. Örneğin 😀 tek bir kod noktasıdır; birleşik emojiler (ör. 👨‍👩‍👧) birden fazla kod noktası sayılır. Bu ifade 2. aşamada Codex'in notu üzerine düzeltildi; ilk sürümde "bir emoji tek karakter sayılır" yazıyordu.
  - Kırpmada Unicode boşlukları dikkate alınır.
  - Ad ve e-postada kontrol karakterleri reddedilir. Açıklamada sekme ve satır sonu serbesttir.
  - Bu kural görevde yoktu. Gerekçesi: PostgreSQL metinde NUL karakterini saklayamaz; bu, `invalid byte sequence for encoding "UTF8": 0x00` hatasıyla doğrulandı. Kural olmasaydı doğrulamadan geçen bir girdi kayıt sırasında 500 hatasına dönüşebilirdi.
  - Geçersiz UTF-16 kaçışları (tek başına `\ud800`) "geçersiz karakter" hatası olarak döner.
- **E-posta:** RFC 5322'nin tamamı uygulanmadı; pratik bir kural seçildi. Kural boşluk, kontrol karakteri ve ikinci bir `@` içermeyen bir yerel kısım ister; alan adında en az bir nokta olmalı ve boş etiket bulunmamalıdır. Bu kural alan adında `/` gibi karakterleri kabul ediyordu; 2. aşamada sıkılaştırıldı (aşağıya bakın).
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

- Adayın uygulamayı kullanarak yapacağı manuel test bu aşamanın sonunda yapılmamıştı.
- Codex'in kod ve test incelemesi bu aşamanın sonunda yapılmamıştı. İnceleme daha sonra yapıldı; aşağıdaki "Aşama 1 Codex incelemesi" bölümüne bakın.
- Gerçek bir ekran okuyucuyla (VoiceOver vb.) test yapılmadı; yalnızca erişilebilirlik ağacı okundu.
- Yalnızca masaüstü uygulamasının yerleşik tarayıcısı denendi; Safari ve Firefox denenmedi.
- Gerçek PostgreSQL'e bağlanan otomatik test yok; bu kontroller komut satırından yapıldı.
- Render ve Neon üzerinde çalıştırma denenmedi; bu aşamanın kapsamı dışında.
- Docker dışında `dotnet run` ile çalıştırma denenmedi.

### Önerilerin kabulü, değiştirilmesi ve reddi

Bu bölüm 1. aşamanın sonunda boştu. Codex'in incelemesi ve adayın bu incelemeye göre verdiği düzeltme kararları aşağıdaki "Aşama 1 Codex incelemesi" ve "Aşama 2" bölümlerinde kayıtlı.

### Commit'ler (saat +03)

| Commit | Saat | İçerik |
| --- | --- | --- |
| `15d44bf` | 13:02 | .NET 10 çözümü, web ve test projeleri, `.gitignore` |
| `de955c1` | 13:12 | `POST /api/requests`, PostgreSQL erişimi, şema, Docker Compose |
| `9d993e0` | 13:14 | Doğrulama ve API testleri |
| `8b70bdb` | 13:22 | Form ve tarayıcı tarafı doğrulama, istemci testleri |
| `383b8a7` | 13:31 | Başlangıç hatasında 1 koduyla çıkış ve compose `restart: on-failure` |
| `c6779b8` | 13:32 | README ve AI_LOG |

## Aşama 1 Codex incelemesi

Bu bölüm, Codex'in 1. aşama inceleme notunun (`/private/tmp/enteksis-phase1-review-20261008.md`) özetidir. Buradaki kontrolleri Codex yaptı. Claude bunları tekrarlamadı; yalnızca iki bulguyu düzeltmeden önce kendisi yeniden üretti (2. aşama bölümüne bakın). Bu kontroller adayın kişisel manuel testi değildir.

- **Kapsam:** İnceleme 8 Ekim 2026'da `c6779b8` commit'i üzerinde yapıldı. Notta belirtildiğine göre Codex kodu değiştirmedi, commit ya da push yapmadı ve bulut kaynaklarına erişmedi.
- **Codex'in doğruladıkları (nota göre):**
  - Build sıfır uyarıyla tamamlandı; `dotnet test` 68/68 ve `node --test` 46/46 geçti.
  - API testlerinin gerçek veritabanı yerine sahte depo kullandığı koddan doğrulandı.
  - Gerçek PostgreSQL'de 201 ve UUID döndü; kayıt kırpılmış değerlerle bulundu.
  - Hatalı istekler 400 aldı ve kayıt sayısı değişmedi.
  - Veritabanı kesintisinde genel mesajlı bir 500 döndü; yanıtta SQL, bağlantı dizesi ya da stack trace yoktu.
  - Yeniden başlatmadan sonra kayıtlar korundu.
  - Ağ erişimi olmayan geçici bir konteynerde başlangıç hatası yaklaşık 0,32 saniyede çıkış kodu 1 ile sonuçlandı. PID 1 sorununun kök nedeni doğrulanmadı.
  - Tarayıcıda boş formda dört alan hatası gösterildi; geçerli gönderimin kaydı veritabanında bulundu.
- **Bulgu 1 (P2):** Gönderim sırasında form alanları düzenlenebiliyordu. Başarı yanıtından sonra `form.reset()` beklerken yazılanları da siliyordu. Kanıt kaydı: `88ae8390-584a-48d8-88e2-3635f44ed870`.
- **Bulgu 2 (P2):** `codex-review@exa/mple.com` adresi hem istemcide hem API'de kabul edilip kaydedildi. Kanıt kaydı: `8f6b47cc-6a98-4597-894a-e37e1b06e634`.
- **Küçük notlar:**
  - Genel 500 mesajı kaydın yapılmadığını kesin bir dille söylüyordu.
  - PID 1 açıklaması kök neden bulunmuş gibi genellenmemeli.
  - Emoji ifadesi düzeltilmeli.
  - Hız sınırı, istek boyutu sınırı, güvenlik başlıkları, gerçek veritabanıyla otomatik test ve yayın kontrolleri henüz tamamlanmış sayılmamalı.
- **Veri:** Codex incelemede 3 kurgusal kayıt ekledi ve toplam 9 oldu; önceki kayıtlar silinmedi.

## Aşama 2: İnceleme düzeltmeleri ve tanıtım sayfası

Aday, inceleme düzeltmelerinin yapılmasını ve ardından tanıtım sayfasının tamamlanmasını istedi. Claude bu aşamanın ilk komutunu 2026-10-08 14:31:30 (+03) saatinde çalıştırdı.

### Görev özeti

Aday mesajının özeti:

- **İnceleme düzeltmeleri:**
  - Gönderim sırasında dört alan devre dışı kalacak, işlem bitince yeniden açılacak ve hatalarda girdiler korunacak. Bu, geciktirilmiş yanıtla tarayıcıda doğrulanacak.
  - `codex-review@exa/mple.com` adresinin kabul edilmesi, basit ve belgelenmiş bir alan adı kuralıyla düzeltilecek. C# ve JavaScript kuralları aynı olacak; alt alan adları ve `+` içeren adresler çalışmaya devam edecek. Doğrulama ve API testleri eklenecek.
  - 500 mesajı, PID 1 açıklaması ve Unicode sayımıyla ilgili küçük düzeltmeler yapılacak.
  - Gözlenmemiş bir senaryo test edilmiş gibi yazılmayacak.
- **Tanıtım sayfası:** Aynı `wwwroot` yapısında, Türkçe ve mobil uyumlu tek bir sayfa. Bölümler sırasıyla:
  - giriş ve "Talep oluştur" bağlantısı
  - form seçenekleriyle birebir eşleşen üç hizmet kartı
  - kısa ve kurgusal bir önce/sonra örneği
  - üç adımlık süreç
  - mevcut form
- **Sayfa koşulları:**
  - Erişilebilirlik: başlık hiyerarşisi, kontrast, görünür odak, alan etiketleri ve durum mesajları.
  - 320 px genişlikte yatay taşma olmayacak.
  - Formun yanında kurgusal bilgi kullanılması gerektiği yazacak.
  - Gerçek müşteri, referans, başarı oranı ya da ölçülmüş kazanç uydurulmayacak; gerçek LLM entegrasyonu eklenmeyecek.
- **Kapsam dışı:** Render ve Neon bağlantısı, yayın, remote ve push, yönetim paneli, kapsamlı idempotency altyapısı.
- **Belgeler ve Git:**
  - Codex incelemesi, Claude'un kontrolleri ve adayın kişisel manuel testi ayrı ayrı kaydedilecek.
  - Aday bildirmeden kişisel manuel testi tamamlanmış sayılmayacak.
  - Anlamlı İngilizce commit'ler kullanılacak; geçmiş commit'ler yeniden yazılmayacak.

### İnceleme bulgularının düzeltilmesi

#### 1. Gönderim sırasında düzenlenebilen alanlar (`61f8bb0`)

- **Yeniden üretme:** Claude bulguyu önce kendisi üretti. Tarayıcıda `fetch` geçici olarak bekletildi; sunucuya istek gitmedi. Bekleme sırasında dört alanın da düzenlenebilir olduğu ve beklerken değiştirilen açıklamanın başarı yanıtından sonra silindiği görüldü.
- **Düzeltme:** `setSubmitting()` artık dört alanı butonla birlikte kapatıyor ve sonuç ne olursa olsun yeniden açıyor. Devre dışı alanlara görünür bir stil eklendi.
- **Doğrulama:** Claude bunları tarayıcıda yaptı; insan testi değildir.
  - **Gerçek gecikme:** Ayrı bir psql oturumu tabloyu 20 saniye boyunca `ACCESS EXCLUSIVE` kilidiyle tuttu.
    - Bekleme sırasında dört alan ve buton devre dışıydı; "Gönderiliyor…" yazısı görünüyordu ve `aria-busy` değeri `true` idi.
    - Açıklama alanına tıklayıp yazmayı denemek değeri değiştirmedi.
    - Kilit açılınca başarı mesajı geldi, alanlar yeniden açıldı ve form temizlendi.
    - Kayıt (`62e20df4-2724-4f0a-85a5-1086f3e1eb94`) psql ile bulundu ve yalnızca gönderilen açıklamayı içeriyordu.
  - **Simüle edilmiş gecikmeli 500:** `fetch` 3 saniye sonra 500 döndürdü; istek gönderilmedi. Bekleme sırasında alanlar kapalıydı, sonra açıldı; girdiler korundu ve uyarı gösterildi.
  - **Gerçek ağ hatası (uygulama konteyneri durduruldu):** Alanlar açıldı ve girdiler korundu.
- **Not:** İlk doğrulama denemesinde tarayıcı önbellekteki eski `app.js` dosyasını çalıştırdı (bkz. 2. madde). Bu deneme bulguyu gerçek gecikmeyle bir kez daha gösterdi: kayıt `568644a5-5f98-4cc5-8166-fd54d4fac8bd` gönderilen açıklamayı içeriyor; beklerken eklenen " EK" ise form temizlenince kayboldu.

#### 2. Önbellekteki eski script (`40c768e`)

Bu sorunu Claude doğrulama sırasında buldu.

- **Gözlem:** Konteyner yeniden derlendikten sonra sunucu yeni `app.js` dosyasını veriyordu. Ancak yanıtta `Cache-Control` başlığı olmadığı için tarayıcı eski kopyayı kendi tahminine göre taze sayıp kullanmaya devam etti.
- **Düzeltme:** Statik dosyalar `Cache-Control: no-cache` başlığıyla sunuluyor; tarayıcı her yüklemede dosyayı ETag ile yeniden doğruluyor.
- **Doğrulama:** Başlık curl ile görüldü. `If-None-Match` ile yapılan istek `304` döndü. Dört statik yol için test eklendi.
- **Not:** Bu başlık eklenmeden önce önbelleğe alınmış kopyalar için sayfanın bir kez zorla yenilenmesi gerekebilir. Claude kendi tarayıcısındaki kopyaları `fetch(..., { cache: "reload" })` ile yeniledi.

#### 3. E-posta alan adı kuralı (`6ea019f`)

- **Önce testler:** Yeni durumlar yazıldı ve eski kuralda başarısız oldukları görüldü: .NET'te 6 ret durumu ve 1 API testi, Node'da 6 ret durumu.
- **Yeni kural (C# ve JavaScript'te aynı desen):** Alan adı noktayla ayrılmış en az iki etiketten oluşur. Her etiket harf ya da rakamla başlayıp biter; arada harf, rakam ve tire bulunabilir.
  - Reddedilenler: `/`, `<`, `>`, `_`, tireyle başlayan ya da biten etiketler ve boş etiketler.
  - Kabul edilenler: alt alan adları, `+` içeren adresler, Unicode harfli alan adları (`örnek.com.tr`) ve punycode (`xn--…`).
  - Yerel kısım değişmedi; README'de bilinen eksik olarak not edildi.
- **Doğrulama:** O sırada .NET 84/84 ve Node 56/56 geçti. Çalışan API'de `codex-review@exa/mple.com` ve `deniz@exa<mple.com` 400 aldı; `deniz+test@mail.example.com.tr` ve `deniz@örnek.com.tr` 201 aldı.

#### 4. Küçük düzeltmeler (`4b472fe`)

- **500 mesajı:** "Talebinizin kaydedildiğini doğrulayamadık. Lütfen bir süre sonra tekrar deneyin." oldu.
  - COMMIT'ten sonra bağlantının kopması senaryosu denenmedi; yeni ifade kod yolundaki olası sonuca dayanıyor.
  - Veritabanı durdurularak çalışan API'de yeni mesaj görüldü.
  - Otomatik tekrar eklenmedi.
- **PID 1:** `Program.cs` yorumu yalnızca Docker denemesinde gözleneni anlatıyor ve kök neden iddiası taşımıyor.
- **Unicode:** İfadeler "kod noktası" olarak düzeltildi. Birleşik bir emojinin (👨‍👩‍👧, 5 kod noktası) birden fazla sayıldığını gösteren testler .NET'e ve Node'a eklendi: ad alanında 20 tanesi kabul ediliyor (100 kod noktası), 21 tanesi reddediliyor (105).

### Tanıtım sayfası (`2583168`)

Claude'un verdiği kararlar:

- **Şirket adı:** Kurgusal şirkete "Örnek Otomasyon" adı verildi. Bu adla şirketin kurgu olduğu açıkça anlaşılıyor ve gerçek bir şirket taklit edilmiyor.
- **İçerik:**
  - Bölümler istenen sırada.
  - Örnek bölümü kurgusal bir klima servis işletmesini anlatıyor. Bölümün kurgusal olduğu ve "süre ya da maliyet kazancı ölçülmedi" sayfada açıkça yazıyor.
  - Önerilen akışın her adımında ilgili hizmetin etiketi var.
  - AI sınıflandırma "yalnızca öneri yapar, son karar ekiptedir" diye anlatıldı.
  - Altbilgide demoda yapay zekâ ya da otomasyon çalıştırılmadığı belirtiliyor.
- **Tasarım:** Sistem fontları kullanıldı; harici font, script ya da görsel yok. Açık renkli kartlar ve paneller var; dar ekranda düzen tek sütuna iniyor.
- **Erişilebilirlik:**
  - Tek bir `h1` ve altında `h2`/`h3` hiyerarşisi.
  - Atlama bağlantısı ve `:focus-visible` ile 3 px odak çerçevesi.
  - Stil verilmiş listelerde `role="list"`.
  - Alan etiketleri, `aria-describedby` bağlantıları ve `status`/`alert` bölgeleri.
  - `prefers-reduced-motion` açıkken yumuşak kaydırma kapanıyor.
- **Form:** ID'leri değiştirilmeden yeni bölüme taşındı. Demo uyarısının tam metni korundu ve yanına örnek bir adres ipucu eklendi.

### Claude'un yaptığı kontroller

Bu kontroller insan testi değildir.

#### Otomatik testler (aşama sonu)

| Komut | Sonuç |
| --- | --- |
| `dotnet build ServiceRequests.slnx` | Başarılı; 0 uyarı, 0 hata |
| `dotnet test ServiceRequests.slnx` | 89/89 başarılı: `ServiceRequestValidatorTests` 69, `ServiceRequestEndpointTests` 20 |
| `node --test tests/client/validation.test.mjs` | 58/58 başarılı |

#### Komut satırıyla yapılan entegrasyon kontrolleri

- E-posta kuralı çalışan API'de denendi; sonuçlar yukarıdaki 3. maddede.
- Veritabanı durdurulmuşken API yeni 500 mesajını döndürdü.
- Statik dosyalarda `Cache-Control: no-cache` başlığı vardı; ETag ile yapılan istek `304` döndü.
- Gecikme için tablo psql ile kilitlendi; ilgili kayıtlar `62e20df4-…` ve `568644a5-…` psql ile bulundu.

#### Tarayıcıda yapılan arayüz kontrolleri

| Kontrol | Sonuç |
| --- | --- |
| Masaüstü (1024×768) | Yatay taşma yok; bölümler istenen sırada. Başlık yapısı: 1 `h1`, 4 `h2` ve altlarında `h3`'ler. Kart başlıkları form seçenekleriyle aynı. |
| 320 px genişlik | Yatay taşma yok ve sağ kenarı aşan öğe yok. Kartlar, paneller ve adımlar tek sütunda. |
| Klavye: atlama bağlantısı | İlk Tab'da 3 px çerçeveyle göründü; Enter ile odak `main` öğesine geçti. |
| Klavye: "Talep oluştur" | Enter ile `#talep` bölümüne gidildi; sonraki Tab ilk alana geçti. Sıra: Ad soyad → E-posta → Hizmet → Açıklama → Talebi gönder. Her öğede 3 px çerçeve vardı. |
| Klavye: boş gönderim | Dört alan hatası göründü; odak Ad soyad alanına geçti. |
| Klavye: geçerli gönderim | Talep numarasıyla başarı mesajı göründü, odak bu mesaja geçti ve form temizlendi. Kayıt (`0ea4ee4a-ee2a-43c1-b7e7-41171334a186`) psql ile bulundu. Hizmet `form_input` ile seçildi; aşağıdaki nota bakın. |
| Gerçek veritabanı hatası | Yeni 500 mesajı göründü; girdiler korundu, alanlar açık kaldı ve odak uyarıya geçti. |
| DOM denetimi | Dört alanın da etiketi var ve `aria-describedby` hedeflerinin hepsi mevcut. Form demo uyarısıyla ilişkili ve `lang="tr"` tanımlı. Konsolda yalnızca bilerek oluşturulan 500 hatası var. |
| Kontrast (Node betiğiyle hesaplandı) | Metin çiftlerinin en düşüğü 5,77:1; metin dışı öğelerin (alan kenarlığı, odak çerçevesi) en düşüğü 4,74:1. |

Aşağıdakiler test aracının sınırlarıdır, sayfa sorunu değildir:

- İlk klavye denemelerinde araç tuşları metin henüz işlenmeden gönderdi, bu yüzden Tab bir alanda kaldı. Tuşlar arasında 1 saniye beklenince sıra doğru çalıştı.
- Araç, yerel `<select>` öğesinde harf yazarak seçim yapamadı; metin son metin alanına eklendi. Bu yüzden hizmet `form_input` ile seçildi. Seçim kutusunun klavyeyle kullanımı tarayıcının yerel davranışıdır ve bu araçla doğrulanamadı.

#### Yapılamayan veya doğrulanamayan kontroller

- Adayın kişisel manuel testinin durumu aşağıdaki bölümde.
- Codex'in 2. aşama incelemesi henüz yapılmadı.
- Gerçek bir ekran okuyucuyla, Safari'de ya da Firefox'ta test yapılmadı.
- Yerel seçim kutusunun klavyeyle kullanımı araçla doğrulanamadı.
- COMMIT'ten sonra bağlantının kopması senaryosu yeniden üretilmedi.
- PID 1 sorununun kök nedeni araştırılmadı.
- Render ve Neon bu aşamanın kapsamı dışında.

### Commit'ler (saat +03)

| Commit | Saat | İçerik |
| --- | --- | --- |
| `61f8bb0` | 14:35 | Gönderim sırasında dört alanın devre dışı bırakılması |
| `40c768e` | 14:35 | Statik dosyalarda `Cache-Control: no-cache` ve testi |
| `6ea019f` | 14:37 | E-posta alan adı kuralı, .NET/Node/API testleri, README |
| `4b472fe` | 14:38 | 500 mesajı, PID 1 yorumu, Unicode ifadeleri ve birleşik emoji testleri |
| `2583168` | 14:46 | Tanıtım sayfası, stiller ve sayfa testleri |
| `4abd361` | 14:50 | README ve AI_LOG |

## Aşama 2 Codex incelemesi

Bu bölüm, Codex'in 2. aşama inceleme notunun (`/private/tmp/enteksis-phase2-review.md`) özetidir. Buradaki kontrolleri Codex yaptı; bunlar adayın kişisel manuel testi değildir.

- **Kapsam:** İnceleme 8 Ekim 2026'da `4abd361` commit'i üzerinde yapıldı. Codex kodu değiştirmedi, commit ya da push yapmadı ve Render ile Neon'a erişmedi.
- **Son teslim:** Notta adayın bildirdiği kesin son teslim zamanı 9 Ekim 2026, 12:49:46 (İstanbul) olarak yazıyor.
- **Codex'in doğruladıkları (nota göre):**
  - Derlemede 0 uyarı vardı; .NET testleri 89/89 ve JavaScript testleri 58/58 geçti.
  - INSERT 15 saniye geciktirildiğinde, gönderim sırasında dört alan ve düğme kilitli kaldı. Başarıdan sonra kontroller yeniden açıldı, form sıfırlandı ve odak başarı mesajına geçti.
  - Başarı mesajındaki kimlik gerçek veritabanında bulundu.
  - `codex-phase2@exa/mple.com` adresi 400 aldı.
  - `/app.js` yanıtında `no-cache` ve ETag vardı; ETag ile yapılan istek 304 döndü.
  - 1280 px ve 320 px genişlikte yatay taşma yoktu.
  - Tab ile alanlar arasında gezilebildi. Otomasyon aracında ArrowDown tuşu hizmet seçimini değiştirmedi; yerel seçim kutusunun tamamen klavyeyle kullanılabildiği doğrulanmış sayılmamalı.
- **Bulgular (3. aşamada tamamlanacak):**
  - `codex<phase2>@example.com` istemcide kabul edildi ve sunucuda 201 ile kaydedildi. Kanıt kaydı: `0006d8e9-bda4-4860-b084-3a9b1cc2aa5d`. Notta ayrıca bu örnekten bir XSS açığı olduğu sonucunun çıkarılmaması gerektiği yazıyor, çünkü veri `textContent` ile gösteriliyor ve listelenmiyor.
  - `codex@𐐀.example` adresini JavaScript kabul etti, C# API ise 400 döndü; iki taraf tutarsızdı.
  - İçerik notu: "Hizmetler" bölümünün giriş cümlesi ürünün faydası yerine formun yapısını anlatıyordu.
- **Veri:** Codex incelemede iki kurgusal kayıt ekledi ve toplam 16 oldu; önceki kayıtlar silinmedi.

## Aşama 3: Doğrulama, sınırlar, güvenlik başlıkları, gerçek veritabanı testleri ve yayın hazırlığı

Aday, `/private/tmp/enteksis-phase3-task.md` dosyasındaki görevin uygulanmasını istedi. Claude bu aşamanın ilk komutunu 2026-10-08 20:10:29 (+03) saatinde çalıştırdı. Yayın yapılmadı, Render ve Neon'a bağlanılmadı; remote oluşturulmadı ve push yapılmadı.

### Görev özeti

Görev dosyasının özeti:

- **E-posta:**
  - `@` öncesi için basit bir ASCII dot-atom kuralı: en fazla 64 karakter; nokta konumları, boşluk, kontrol karakteri, `<` ve `>` denetlenecek.
  - Unicode alan adı desteği korunacak; C# ve JavaScript aynı kod noktası mantığını kullanacak ve `codex@𐐀.example` iki tarafta aynı sonucu verecek.
  - Biçim kontrolünün adresin varlığını kanıtlamadığı belirtilecek.
  - "Hizmetler" metni düzeltilecek.
- **API sınırları:**
  - Yalnızca `POST /api/requests` için ortak bir sayaçla 60 saniyede 20 istek, kuyruk 0. Aşılırsa 429, Türkçe ProblemDetails ve `Retry-After`.
  - 32 KiB gövde sınırı: `Content-Length` ile ve chunked gövdelerde 413; doğrulama gerçek Kestrel'de de yapılacak.
  - Arayüz 429 ve 413'ü anlatacak; 30 saniyelik istemci zaman aşımı eklenecek; otomatik yeniden gönderim olmayacak.
- **Güvenlik başlıkları:** CSP (`self` ve `none` yönergeleri), `nosniff` ve `no-referrer`; hata yanıtlarında da bulunacak. HTTPS yönlendirmesi ya da proxy güven ayarı eklenmeyecek.
- **Gerçek PostgreSQL testleri:**
  - Ayrı bir projede Testcontainers ile PostgreSQL 17.
  - Senaryolar: geçerli istekte 201 ve başka bağlantıdan okunabilen kırpılmış kayıt; geçersiz istekte kayıt olmaması; veritabanı kesilince ayrıntı sızdırmayan genel 500; yeni host'ta önceki kaydın korunması.
  - Ayrıca 429, 413 ve başlık testleri.
  - Docker yokken testler sessizce geçmeyecek.
- **Konteyner:**
  - Resmî paket deposundan tini eklenecek; uygulama yine root olmayan kullanıcıyla çalışacak.
  - `GET /health`: yalnızca uygulamanın yanıt verdiğini ve `RENDER_GIT_COMMIT` değerini gösterecek, sahte SHA üretilmeyecek.
- **Yayın hazırlığı:**
  - Render ve Neon (Frankfurt) için README adımları yazılacak; yer tutucular kullanılacak ve gerçek parola istenmeyecek.
  - Gizli bilgi taraması yapılacak.
  - Teslim ekranının beş alanı için bir taslak hazırlanacak; olmayan URL ya da SHA uydurulmayacak.

### Claude'un kararları ve bulguları

- **E-posta:**
  - Yerel kısım için C#'ta `\z` ile biten bir desen kullanıldı. .NET'te `$` sondaki bir `\n`'den önce de eşleştiği için `$` kullanılsaydı `deniz\n@example.com` kabul edilebilirdi; bu durum için test eklendi.
  - Alan adı iki tarafta da kod noktası düzeyinde denetleniyor: C#'ta `Rune.IsLetter`/`Rune.IsDigit`, JavaScript'te `u` bayraklı `\p{L}`/`\p{Nd}`.
  - `@` öncesindeki 64 karakter sınırı için ayrı bir hata mesajı eklendi.
- **Gövde sınırı:**
  - Gövde, sunucudan bağımsız olarak en fazla 32 KiB + 1 bayt okunuyor; bu yöntem chunked gövdelerde de çalışıyor.
  - `Content-Length` 32 KiB'tan büyükse gövde hiç okunmadan 413 dönüyor.
  - Sunucunun bozuk gövde için verdiği hata kendi durum koduyla dönüyor, 500'e çevrilmiyor.
- **Hız sınırı:** `AddFixedWindowLimiter` tek bir bölmeyle kullanıldı, yani sayaç ortak. `Retry-After` değeri sınırlayıcının bilgisinden alınıyor; bilgi yoksa pencere süresi (60 sn) kullanılıyor.
- **Güvenlik başlıkları:** Başlıklar ilk ara katmanda `Response.OnStarting` içinde ekleniyor. Böylece exception handler'ın başlıkları temizlediği yanıtlarda da bulunuyorlar.
- **Testler:** Ortak test yardımcıları `TestSupport.cs` dosyasına taşındı. Uzunluğu önceden bilinmeyen gövdeler için `UnknownLengthContent` sınıfı eklendi.

### Claude'un yaptığı kontroller

Bu kontroller insan testi değildir.

#### Önce yeniden üretme ve testlerin başarısız olduğunu görme

- Mevcut kodda `codex<phase2>@example.com` hem istemcide hem API'de kabul edildi (201; kurgusal kayıt `2c2f5122-0a2c-4726-a342-ca5228dcb545`).
- Mevcut kodda `codex@𐐀.example` adresini istemci kabul etti, API 400 döndü.
- Yeni testler mevcut kodda başarısız oldu: .NET'te 12, Node'da 9 test.

#### Otomatik testler (aşama sonu)

| Komut | Sonuç |
| --- | --- |
| `dotnet build ServiceRequests.slnx` | 0 uyarı, 0 hata |
| `dotnet test tests/ServiceRequests.Web.Tests` | 122/122 başarılı: doğrulayıcı 85, API 22, sınırlar 5, güvenlik başlıkları 7, health 3 |
| `dotnet test tests/ServiceRequests.Web.IntegrationTests` | 4/4 başarılı: gerçek PostgreSQL 17 (Testcontainers), yaklaşık 7 sn |
| `dotnet test ServiceRequests.slnx` | 122/122 + 4/4 başarılı |
| `node --test tests/client/validation.test.mjs` | 75/75 başarılı |
| Docker soketi olmayan bir SDK konteynerinde entegrasyon testleri | 4/4 başarısız (`DockerUnavailableException`), çıkış kodu 1. Testler Docker yokken geçmiş sayılmıyor. |

Not: Ana makinede `DOCKER_HOST` var olmayan bir sokete yönlendirildiğinde testler yine geçti, çünkü Testcontainers sıradaki uç noktaya (Docker Desktop) geçti. Bu yüzden "Docker yok" durumu bu yöntemle değil, Docker soketi olmayan bir SDK konteyneriyle denendi.

#### Komut satırıyla yapılan entegrasyon kontrolleri (curl, psql, Docker)

| Kontrol | Sonuç |
| --- | --- |
| E-posta eşlik denetimi: 600 rastgele adres hem tarayıcı modülüne hem çalışan API'ye verildi (adı geçersiz bırakıldı, kayıt oluşmadı) | 0 uyumsuzluk; ikinci 300'lük çalıştırmada 244 adres iki tarafta da geçerli sayıldı. Kayıt sayısı değişmedi. |
| 21 ardışık POST (geçersiz gövdeyle, gerçek Kestrel) | İlk 20 istek 400, 21. istek 429 aldı; `Retry-After: 60` ve Türkçe başlık vardı. Sayfa ve statik dosyalar 200 döndü. Kayıt sayısı değişmedi. |
| Gövde sınırı (gerçek Kestrel) | Tam 32 KiB gövde okunup doğrulandı (400). 32 KiB + 1 bayt hem `Content-Length` ile hem chunked gönderimde 413 aldı. 5 MiB chunked gövde de 413 aldı ve uygulama çalışmaya devam etti. |
| Güvenlik başlıkları (curl) | `/`, `/app.js`, 404 ve 400 yanıtlarında CSP, `nosniff` ve `no-referrer` vardı. |
| tini | PID 1 `/usr/bin/tini -- dotnet ServiceRequests.Web.dll`, kullanıcı UID 1654 (`app`). `docker compose stop` 1 saniyeden kısa sürede tamamlandı ve çıkış kodu 0 oldu. Veritabanı kapalıyken uygulama 1 koduyla çıktı ve yeniden başlatıldı (CPU %0, `Unhandled exception` yok); veritabanı açılınca kalktı. |
| `/health` | `{"status":"ok","commit":null}` döndü (yerelde `RENDER_GIT_COMMIT` yok). |
| Gizli bilgi taraması (`git grep`, değerler yazdırılmadan) | Gerçek sır bulunmadı. Eşleşmeler yalnızca Compose ve `.env.example` içindeki yerel geliştirme örnek parolası, README'deki yer tutucu ve testteki sahte bir değerdi. Neon adresi, `postgres://` URI'si, özel anahtar ya da API anahtarı yok. Geçmişe hiç `.env` eklenmemiş. `.gitignore` ve `.dockerignore` `.env` dosyalarını dışarıda bırakıyor. |

#### Tarayıcıda yapılan arayüz kontrolleri

| Kontrol | Sonuç |
| --- | --- |
| Gerçek 429 (pencere önce curl ile dolduruldu) | "…talebiniz kaydedilmedi. Lütfen yaklaşık 60 saniye sonra tekrar deneyin…" mesajı göründü; alanlar açıldı, değerler korundu, odak uyarıya geçti. |
| Gerçek zaman aşımı (psql 45 sn tablo kilidi) | Beklerken alanlar kilitliydi. 30. saniyede "…kaydedilip kaydedilmediğini bilmiyoruz…" mesajı göründü; alanlar açıldı ve değerler korundu. Bu denemede bekleyen INSERT iptal edildi ve kayıt oluşmadı; bunun her durumda böyle olacağı garanti değildir. |
| Simüle edilmiş 413 | Boyut mesajı göründü, değerler korundu. |
| CSP etkinken sayfa | Sayfa, `styles.css`, `app.js` ve `validation.js` 200 ile yüklendi. Stil uygulandı (düğme rengi), script çalıştı (boş gönderimde 4 hata) ve gerçek bir POST başarılı oldu (`e6bab0b2-bff4-40c7-8871-265cf893e4ad`, psql ile bulundu). Konsolda CSP ihlali yoktu. |

#### Yapılamayan veya doğrulanamayan kontroller

- Render ve Neon üzerinde çalıştırma, HTTPS davranışı ve gerçek Neon TLS bağlantısı (`VerifyFull`) denenmedi; bunlar yayın aşamasında yapılacak.
- Codex'in 3. aşama incelemesi henüz yapılmadı.
- Gerçek bir ekran okuyucuyla, Safari'de ya da Firefox'ta test yapılmadı.
- Tini'nin başlangıç dışındaki çökme durumlarındaki davranışı denenmedi. Eski CPU takılmasının kök nedeni araştırılmadı.

### Commit'ler (saat +03)

| Commit | Saat | İçerik |
| --- | --- | --- |
| `c6a54ca` | 20:13 | E-posta: yerel kısım için dot-atom kuralı, alan adında kod noktası denetimi, testler |
| `d695bfe` | 20:14 | "Hizmetler" giriş metni |
| `bad713a` | 20:19 | Hız sınırı (20/60 sn) ve 32 KiB gövde sınırı, testler |
| `a3ad04c` | 20:21 | Arayüzde 429/413 mesajları ve 30 saniyelik zaman aşımı |
| `f3152e3` | 20:22 | CSP, `nosniff`, `Referrer-Policy` ve testleri |
| `025c73f` | 20:23 | `GET /health` ve testleri |
| `ab2ed98` | 20:24 | Konteynerde tini |
| `f4e0437` | 20:27 | Testcontainers ile gerçek PostgreSQL testleri |
| Bu bölümün eklendiği commit | — | README ve AI_LOG |

## Adayın kişisel manuel testi

Bu bölümde yalnızca adayın kendisinin bildirdiği kontroller yer alır.

| Aşama | Durum |
| --- | --- |
| 1. aşama | Aday ayrı bir kişisel manuel test bildirmedi; tamamlanmış sayılmıyor. |
| 2. aşama (`4abd361` sürümü) | Aday uygulamayı kullandı, başarılı bir gönderim gördü ve "Bi sorun göremedim" diyerek sorun bildirmedi. Arayüzde gördüğü talep numarası `590370d6-0e9b-4b2f-98e3-ede44a8721a6` idi. Bu kaydı yerel PostgreSQL'de aday değil Codex sorguladı: hizmet `api-integration`, kayıt zamanı 8 Ekim 2026 20:02:14.993336 (İstanbul). Kullanılan tarayıcı ve diğer manuel test adımları tek tek bildirilmedi; bunlar yapılmış sayılmıyor. |
| 3. aşama | Aday kişisel manuel test bildirmedi; tamamlanmış sayılmıyor. |
