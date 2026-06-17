# Pliaforma Tasarım Sistemi

Hedef his: **sade, ferah, yumuşak, premium**. Pilates/wellness estetiği — göz yormayan, davetkâr.
Koyu/boğucu yüzeylerden kaçınılır; bol beyaz alan, yumuşak köşeler, ince gölge.

## Palet — Blush/Rose + Krem

Token'lar CSS değişkeni olarak `globals.css`'te tanımlı; Tailwind'e `@theme inline` ile bağlı.
`bg-background`, `text-foreground`, `bg-primary`, `border-border`, `bg-surface-muted` gibi kullanılır.
Light/dark iki set; `.dark` sınıfı `<html>`'e eklenir (flash önleyici inline script).

| Token | Light | Dark | Kullanım |
|---|---|---|---|
| `background` | `#faf6f2` krem | `#181317` | sayfa zemini |
| `surface` | `#ffffff` | `#221d21` | kart/yüzey |
| `surface-muted` | `#f6efea` | `#2b2429` | ikincil yüzey |
| `foreground` | `#3d3438` | `#f0e8e9` | metin |
| `muted` | `#8a7d81` | `#a99ca0` | ikincil metin |
| `border` | `#ece1db` | `#342c31` | kenarlık |
| `primary` | `#c97c8a` dusty rose | `#e0909c` | CTA, vurgu |
| `primary-hover` | `#bd6b7a` | `#e9a1ac` | hover |
| `primary-soft` | `#f7e7e9` blush | `#3a2a30` | rozet/aktif nav zemini |
| `success/warning/danger` | yumuşak yeşil/amber/gül | — | durum |

## İlkeler
- **Köşeler:** `rounded-xl` (kontrol), `rounded-2xl` (kart/modal).
- **Gölge:** yalnızca `shadow-sm`; modal'da `shadow-xl`.
- **Odak:** yumuşak `ring` (`--ring`), `outline-offset: 2px`.
- **Tipografi:** Nunito Sans (yumuşak grotesk); başlık `font-semibold`, gövde `text-sm`.
- **Hareket:** kısa `transition-colors` (~150ms); abartısız.
- **Responsive:** mobil öncelikli; sidebar `lg:` üstünde görünür.

## Component sistemi (`src/components/ui`)
`Button` (primary/secondary/ghost/danger · sm/md/lg · loading), `Input`/`Textarea`, `Select`, `Card`/
`CardHeader`, `Badge` (rose/neutral/success/warning/danger), `Alert`, `FormField` (label+hint+error),
`Modal`, `Table` (THead/TR/TH/TD/TableEmpty).

Layout: `Sidebar`, `Header`, `ThemeToggle`, `Brand`, `FullscreenLoader`.
