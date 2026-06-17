# Kodlama Konvansiyonları

## Genel
- `Directory.Build.props`: `net10.0`, Nullable, ImplicitUsings, **TreatWarningsAsErrors=true**.
- `Directory.Packages.props`: Central Package Management — sürümler tek yerde, csproj'larda sürüm yok.
- Klasik `.sln` (Visual Studio uyumluluğu).

## Feature (CQRS) yazımı
- Her komut/sorgu kendi dosyasında: `record Command/Query` + `Validator` (gerekiyorsa) + `Handler`.
- Konum: `Application/Features/{Alan}/{IsimVerb}.cs`.
- Yazma → `ICommand<T>`; okuma → `IQuery<T>`. Handler `ICommandHandler`/`IQueryHandler` uygular.
- Stüdyo-içi yazma komutları ileride `IStudioWriteRequest` işaretçisini taşır (guard'lar devreye girer).
- Validation FluentValidation ile; iş kuralı ihlalleri Domain exception'larıyla fırlatılır.

## Persistence
- Her entity için `IEntityTypeConfiguration<T>` (Configurations klasörü); `ApplyConfigurationsFromAssembly`.
- Enum'lar DB'de **string** (`HasConversion<string>()`).
- Id `ValueGeneratedNever` (istemci üretir). Soft-delete global filter otomatik.
- Migration adı PascalCase: `dotnet ef migrations add <Ad>`.

## Controller route kuralı (ÖNEMLİ)
- `[Route("api/[controller]")]` token'ı controller adındaki PascalCase'i küçültür **ama tireyi
  EKLEMEZ**: `ClassTypesController` → `/api/classtypes` (beklenen `/api/class-types` DEĞİL).
- **Kural:** çok kelimeli (multi-word) controller'lara **açık kebab-case route** verin:
  `[Route("api/class-types")]`, `[Route("api/studio/memberships")]`. Tek kelimeli controller'lar
  (`Auth`, `Members`, `Instructors`, `Studio`) `[controller]` token'ıyla sorunsuzdur.
- Yeni controller eklerken `/api/<kebab>` bekleniyorsa açık route şarttır (aksi halde sessiz 404).

## Hata yönetimi
- Domain exception'ları (`NotFound/Forbidden/Conflict/BusinessRule/Unauthorized`) +
  Application `ValidationException`. Hepsi `ExceptionHandlingMiddleware`'de `ApiResponse.Fail`'e map.
- Migration/seed hataları YUTULMAZ (fail-fast).

## Soyutlama kuralları
- Application sağlayıcıya bağlanmaz: EF Core **çekirdeği** (DbSet) tamam, `DatabaseFacade`/Npgsql HAYIR.
  Transaction `ITransactionManager` arkasında.
- Dış servisler (token, hash, saat) Application'da arayüz, Infrastructure'da uygulama.
- **Arama/filtre sağlayıcı-bağımsız:** Npgsql'e özgü `EF.Functions.ILike` Application'a girmez (Npgsql'i
  sızdırır). Bunun yerine `x.ToLower().Contains(term)` — EF bunu PostgreSQL'de `lower(x) LIKE '%...%'`'e
  çevirir (case-insensitive, native). Provider-spesifik özellik gerekirse Persistence katmanında kalmalı.

## Paket sürümleri
- `Directory.Packages.props` sürümleri **kurulu .NET shared framework** ile hizalı olmalı (şu an 10.0.2).
  Paketleri runtime'dan ileri bir patch'e sabitlemek (örn. 10.0.4) framework-sağlanan assembly'lerin
  runtime'da yüklenememesine yol açar (`FileNotFoundException`). Npgsql provider'ın EF Core bağımlılığına
  dikkat (10.0.2 → EF 10.0.4 ister; bu yüzden Npgsql 10.0.0 kullanıldı).
- Application, framework-sağlanan `Microsoft.Extensions.*` abstraction'larını **explicit referanslamaz**;
  EF Core'dan transitive gelir (explicit referans aynı yükleme hatasını tetikliyordu).

## Concurrency / atomik kapasite (Sprint 4)
- Sayaç/kapasite gibi yarışa açık güncellemelerde **`COUNT + INSERT` KULLANMA** (race'te patlar).
- **Atomik koşullu UPDATE** kullan: `UPDATE ... SET x = x + 1 WHERE x < limit` → etkilenen satır 0 ise
  reddet. Satır kilidi commit'e kadar tutulur (transaction içinde çağır).
- Sağlayıcıya özgü atomik SQL **Persistence**'ta kalır; Application yalnızca soyut servis arayüzünü görür
  (`IClassSessionCapacityService`). DB-seviyesi benzersizlik için PostgreSQL **filtered (partial) unique
  index** (`HasFilter`) kullanılabilir.

## Ledger (defter) + satır kilidi (Sprint 5)
- Bakiyesi olan kavramlar (telafi kredisi) **tek sayaç alanı yerine hareket defteri** olarak tutulur:
  her olay ayrı kayıt (`MakeupCreditTransaction`), bakiye = `SUM(Amount)` (işaretli). Hareketler **soft
  delete edilmez**; düzeltme için ters hareket (`Revoked`/`Restored`) eklenir → denetlenebilirlik.
- Bakiye tek satırda olmadığından, "son krediyi" iki eşzamanlı işlemin harcamasını engellemek için ilgili
  öğrenci satırı `SELECT ... FOR UPDATE` ile kilitlenir (`IMakeupCreditLock`, Persistence'ta). Kilit aktif
  transaction commit'e kadar tutulur. Raw SQL yine Persistence'ta; Application soyut arayüzü görür.
- Kredi tüketimi/iadesi **booking ile aynı transaction'dadır** (TransactionBehavior) → kısmi durum oluşmaz.

## Tarih/saat & timezone (Sprint 3)
- Stüdyo-yerel "duvar saati" alanları (`ClassSession.StartAt/EndAt`) `timestamp without time zone` ile
  saklanır; DateTime `Kind=Unspecified` (`DateTime.SpecifyKind(..., Unspecified)`). UTC↔TZ dönüşümü
  şimdilik yapılmaz; istemci stüdyo-yerel zaman gönderir.
- Audit alanları (`CreatedAt/UpdatedAt`) UTC'dir (`IDateTime.UtcNow`, `timestamptz`). İkisini karıştırma.
- "Şimdi/geçmiş" karşılaştırması gerektiğinde `Studio.TimeZoneId` ile tek dönüşüm yapılır
  (`SessionRules.StudioNowAsync`). Geçersiz TZ → UTC fallback.
