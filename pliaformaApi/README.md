# Pliaforma API

Pilates stüdyoları için multi-tenant SaaS — backend. .NET 10, Clean Architecture, özel CQRS
(MediatR yok), PostgreSQL + EF Core, JWT.

## Katmanlar
`Pliaforma.Domain` · `Pliaforma.Application` · `Pliaforma.Persistence` · `Pliaforma.Infrastructure` ·
`Pliaforma.Api`. Bağımlılık içe doğru (Api → App/Persistence/Infra → Domain).

## Hızlı başlangıç
```bash
# 1) PostgreSQL (Docker)
docker run -d --name pliaforma-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=pliaforma -p 5432:5432 postgres:17-alpine

# 2) Gizli ayarlar (appsettings'te düz metin tutulmaz)
cd src/Pliaforma.Api
dotnet user-secrets set "Jwt:Secret" "<en az 32 karakter>"
dotnet user-secrets set "Seed:AdminPassword" "<opsiyonel admin parolası>"

# 3) Çalıştır (migration + seed otomatik)
dotnet run --project src/Pliaforma.Api
```
- Swagger: http://localhost:5132/swagger · Health: http://localhost:5132/health

## Auth uçları
`register-studio`, `register`, `login`, `switch-studio`, `refresh`, `logout`, `me`
(detay: [docs/identity-and-membership.md](docs/identity-and-membership.md)).

## Same-origin & Compose
Frontend API'ye doğrudan gitmez; `/api` same-origin proxy'lenir (dev'de Next rewrite, prod'da nginx).
Tüm stack'i kök klasörden compose ile ayağa kaldırabilirsiniz:
```bash
docker compose -f ../docker-compose.dev.yml up --build      # postgres + backend + frontend
```
Container/prod ortam değişkenleri: [.env.example](.env.example) · kök [deploy/.env.example].

## Auth smoke testi
```bash
scripts/auth-smoke.sh                     # backend doğrudan (:5132)
scripts/auth-smoke.sh http://localhost:3000   # same-origin (frontend proxy)
```
Tüm auth akışlarını doğrular (bkz. [docs/auth-smoke-tests.md](docs/auth-smoke-tests.md)).

## Dokümantasyon
[docs/MIMARI-PLAN.md](docs/MIMARI-PLAN.md) tek doğruluk kaynağıdır. Ayrıca: domain-model,
multi-tenancy, auth-and-roles, conventions, sprints, adr/.

## Sprint 0 durumu
İskelet + Auth/Studio/Membership tamamlandı; build 0 hata/0 uyarı; auth akışı uçtan uca doğrulandı.
