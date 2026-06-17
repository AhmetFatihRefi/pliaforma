# Auth Smoke Testleri

Otomatik test altyapısı henüz yok; bu doküman + `scripts/auth-smoke.sh` manuel doğrulamayı korur.

## Çalıştırma
```bash
# Backend doğrudan
scripts/auth-smoke.sh                       # http://localhost:5132

# Same-origin (frontend proxy üzerinden) — mimariyi de doğrular
scripts/auth-smoke.sh http://localhost:3000
```
Script her akışın HTTP kodunu beklenenle karşılaştırır; hepsi geçerse exit 0, biri başarısızsa exit 1.

## Kapsanan akışlar ve beklenen sonuç

| # | Akış | Beklenen |
|---|---|---|
| 1 | `register-studio` (benzersiz e-posta) | 200 + access token + `pf_refresh` cookie |
| 2 | `login` | 200 + access token + taze cookie |
| 3 | `me` (Bearer) | 200 + doğru stüdyo/rol |
| 4 | `switch-studio` (kendi stüdyosu) | 200 |
| 5 | `refresh` (cookie + studioId) | 200 + rotasyon |
| 6 | `switch-studio` (üye olmayan stüdyo) | 403 |
| 7 | `logout` | 200 + cookie temizlenir |
| 8 | logout sonrası `refresh` | 401 (revoke edilmiş) |
| 9 | çift e-posta ile `register` | 409 |
| 10 | geçersiz gövde ile `register-studio` | 400 + alan hataları |

## Notlar
- Cookie akışı `curl` cookie-jar ile test edilir; tarayıcıda httpOnly cookie aynı şekilde davranır.
- Same-origin doğrulaması için `:3000` ile çalıştırın: istek frontend origin'ine gider, Next rewrite
  backend'e proxy'ler, `Set-Cookie` aynı origin'e (path `/api/auth`) yazılır.
- Her çalıştırma benzersiz e-posta (`smoke+<timestamp>@…`) kullanır; tekrar tekrar çalıştırılabilir.
