# Yerel Geliştirme

## Gereksinimler
- .NET 10 SDK
- PostgreSQL 17 (veya Docker)

## PostgreSQL (Docker ile)
```bash
docker run -d --name pliaforma-postgres \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=pliaforma \
  -p 5432:5432 postgres:17-alpine
```

## Gizli ayarlar (appsettings'te DÜZ METİN TUTULMAZ)
```bash
cd src/Pliaforma.Api
dotnet user-secrets set "Jwt:Secret" "<en az 32 karakter rastgele>"
dotnet user-secrets set "Seed:AdminPassword" "<admin parolası>"   # opsiyonel; verilmezse admin tohumlanmaz
```
> Üretimde bunlar `Jwt__Secret`, `Seed__AdminPassword` ortam değişkenleriyle verilir. Secret yok/kısa ise
> uygulama **açılmaz** (fail-fast).

## Migration & çalıştırma
```bash
# Migration ekleme (gerekince)
dotnet ef migrations add <Ad> --project src/Pliaforma.Persistence --startup-project src/Pliaforma.Api

# Çalıştırma (başlangıçta migration + seed otomatik uygulanır)
dotnet run --project src/Pliaforma.Api
```

- Swagger: `http://localhost:5132/swagger`
- Health: `http://localhost:5132/health`

## Hızlı duman testi
```bash
curl -X POST http://localhost:5132/api/auth/register-studio -H 'Content-Type: application/json' \
  -d '{"studioName":"Zen Pilates","ownerFullName":"Ayşe Y.","email":"ayse@zen.com","password":"Parola123"}'
```
