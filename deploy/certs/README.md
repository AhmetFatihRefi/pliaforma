# TLS sertifikaları (prod)

Bu klasör nginx'in HTTPS sertifikalarını barındırır. **Gerçek sertifika/anahtar dosyaları repo'ya
ASLA eklenmez** (`.gitignore`: `*.pem`, `*.key`, `*.crt`). Bu README yalnız klasörün var olması içindir.

`docker-compose.prod.yml` bu klasörü `/etc/nginx/certs` olarak (read-only) bağlar.
nginx (`deploy/nginx.conf`) şu dosyaları bekler:

- `fullchain.pem`
- `privkey.pem`

## Sertifika alımı (örnek — certbot standalone, prod stack kapalıyken)

```
certbot certonly --standalone -d your-domain.com
cp /etc/letsencrypt/live/your-domain.com/fullchain.pem deploy/certs/fullchain.pem
cp /etc/letsencrypt/live/your-domain.com/privkey.pem   deploy/certs/privkey.pem
```

Dosyalar yoksa nginx açılmaz (HTTPS zorunlu — bilinçli tasarım). Detay:
[pliaformaApi/docs/PILOT-DEPLOY-DRY-RUN.md](../../pliaformaApi/docs/PILOT-DEPLOY-DRY-RUN.md).
