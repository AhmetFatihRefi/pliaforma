# Pliaforma — Orkestrasyon

Pilates stüdyoları için multi-tenant SaaS. İki ayrı repo + bu klasörde deploy glue:

- `pliaformaApi/` — backend (.NET 10) · repo: `pliaforma-backend`
- `pliaformaFrontend/` — frontend (Next.js 16) · repo: `pliaforma-frontend`
- `deploy/` + `docker-compose.*.yml` — orkestrasyon (nginx same-origin)

## Mimari: same-origin `/api`

Tarayıcı API'ye **doğrudan gitmez**. Tüm `/api/*` (ve `/health`) aynı origin'den gider:
- **Development:** Next.js rewrite `/api/*` → backend (`BACKEND_ORIGIN`).
- **Production:** nginx tek giriş — `/` → frontend, `/api/` → backend, `/health` → backend.

Refresh cookie bu sayede **first-party / same-origin** çalışır; mimari CORS'a bağımlı değildir
(CORS yalnızca istisnai doğrudan-erişim için ayarlı kalır).

## Compose ile çalıştırma

```bash
# Geliştirme (postgres + backend + frontend)
docker compose -f docker-compose.dev.yml up --build
#   Frontend: http://localhost:3000   ·   Backend (doğrudan/Swagger): http://localhost:5132/swagger

# Production taslağı (nginx + backend + frontend + postgres)
cp deploy/.env.example deploy/.env      # değerleri doldurun (JWT_SECRET, POSTGRES_PASSWORD, PUBLIC_ORIGIN...)
docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build
#   Tek giriş: http://localhost  (nginx)
```

> Native geliştirme (hot reload) için backend ve frontend'i ayrı ayrı da çalıştırabilirsiniz —
> her repo'nun README'sine bakın. Smoke testi: `pliaformaApi/scripts/auth-smoke.sh`.
