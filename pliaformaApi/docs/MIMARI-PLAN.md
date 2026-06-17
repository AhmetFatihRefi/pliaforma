# Pliaforma — Mimari Plan (Tek Doğruluk Kaynağı)

Pilates stüdyoları için **multi-tenant SaaS**. Backend .NET 10, Clean Architecture + özel CQRS;
frontend Next.js 16. Her stüdyo bir **tenant**; tüm operasyonel veri stüdyo bazında izole.

## Katmanlar (bağımlılık yönü içe doğru)

```
Pliaforma.Api  ──►  Application + Persistence + Infrastructure
Application    ──►  Domain  (yalnız Domain'e bağlı + EF Core ÇEKİRDEĞİ)
Persistence    ──►  Application
Infrastructure ──►  Application
Domain         ──►  (bağımlılıksız)
```

- **Domain:** entity, enum, exception. İş kuralları burada yaşar, hiçbir altyapıya bağlı değildir.
- **Application:** CQRS (komut/sorgu/handler), pipeline behaviors, DTO, soyut arayüzler
  (`IAppDbContext`, `ICurrentUserService`, `ITokenService`, `ITransactionManager`...). EF Core
  **çekirdeğine** (DbSet) bağlıdır ama sağlayıcıya (Npgsql) ve `DatabaseFacade`'e bağlı değildir.
- **Persistence:** `AppDbContext`, EF konfigürasyonları, `AuditInterceptor`, `EfTransactionManager`,
  migration'lar. Npgsql burada.
- **Infrastructure:** JWT (`TokenService`), `PasswordHasher` (BCrypt), `CurrentUserService`, JWT auth.
- **Api:** Controller'lar, `ExceptionHandlingMiddleware`, rate limit, health, refresh cookie, seed, Program.cs.

## CQRS (MediatR YOK)

`IDispatcher` → reflection tabanlı `Dispatcher`. Her istek behavior zincirinden geçer:

```
Logging → Validation → StudioStatus → MembershipGuard → Transaction → Handler
```

- **Validation:** FluentValidation (`ValidationBehavior`), hata → HTTP 400 + alan sözlüğü.
- **StudioStatus / MembershipGuard:** yalnızca `IStudioWriteRequest` taşıyan komutlarda çalışır
  (stüdyo aktif mi + üyelik hâlâ Active mi). Sprint 0'da bu işaretçiyi kimse taşımaz; altyapı hazır.
- **Transaction:** `ICommand` istekleri `ITransactionManager` ile tek transaction'a alınır.

## İstek yaşam döngüsü

```
HTTP → Controller → IDispatcher.Send → [behaviors] → Handler → IAppDbContext/EF → DB
                                                   ↘ ApiResponse<T> zarfı ile döner
```

Hatalar `ExceptionHandlingMiddleware`'de tek tip `ApiResponse.Fail` gövdesine map edilir
(`NotFound→404`, `Unauthorized→401`, `Forbidden→403`, `Conflict→409`, `Validation→400`).

## Kimlik & Tenant (özet)

Global `User` ⇄ `Studio` arası bağ **`StudioMembership`** ile kurulur (rol + durum + yön). JWT;
`sub`, `global_role` ve stüdyo seçiliyse `studio_id` + `studio_role` + `membership_id` taşır.
Ayrıntı: [identity-and-membership.md](identity-and-membership.md), [multi-tenancy.md](multi-tenancy.md).

## Yol haritası

Sprint dökümü: [sprints.md](sprints.md). Sprint 0 = bu temel (Auth/Studio/Membership + frontend shell).
Domain detayları (ileri sprint şemaları dahil): [domain-model.md](domain-model.md).

## Frontend

Next.js 16 (App Router) + Tailwind v4. Access token bellekte, refresh token **httpOnly cookie**.
`lib/types.ts` backend Auth DTO'larını birebir yansıtır (senkron sözleşme). Tasarım:
[../../pliaformaFrontend/docs/design-system.md].
