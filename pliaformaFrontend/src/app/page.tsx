"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { useStudioRegistrationEnabled } from "@/lib/registration";
import { cn } from "@/lib/cn";
import { Brand, PliaformaMark } from "@/components/layout/Brand";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { FullscreenLoader } from "@/components/layout/FullscreenLoader";

/* ------------------------------------------------------------------ */
/*  Pliaforma — Public Landing Page  (route: /)                        */
/*  Giriş yapmış kullanıcılar otomatik /dashboard'a yönlendirilir.     */
/* ------------------------------------------------------------------ */

export default function LandingPage() {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && user) router.replace("/dashboard");
  }, [loading, user, router]);

  // Auth durumu çözülürken / oturum varken landing'i göstermeyelim.
  if (loading || user) return <FullscreenLoader />;

  return (
    <div className="min-h-dvh bg-background text-foreground">
      <LandingHeader />
      <Hero />
      <ValueCards />
      <Features />
      <HowItWorks />
      <Audience />
      <Showcase />
      <Faq />
      <FinalCta />
      <Footer />
      <LandingKeyframes />
    </div>
  );
}

/* ----------------------------- Header ----------------------------- */

const NAV = [
  { href: "#features", label: "Özellikler" },
  { href: "#how", label: "Nasıl Çalışır" },
  { href: "#showcase", label: "Panel" },
  { href: "#faq", label: "SSS" },
];

function LandingHeader() {
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  // Sprint 49: pilotta herkese açık stüdyo kaydı kapalıysa "Stüdyo Hesabı Oluştur" gizlenir; Giriş Yap kalır.
  const showStudioCta = useStudioRegistrationEnabled() !== false;

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 12);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      className={cn(
        "sticky top-0 z-50 border-b transition-all duration-300",
        scrolled
          ? "border-border bg-background/70 shadow-[0_8px_30px_-18px_rgba(0,0,0,0.28)] backdrop-blur-md backdrop-saturate-150"
          : "border-transparent bg-background/50",
      )}
    >
      <div className="mx-auto flex max-w-[1160px] items-center justify-between gap-6 px-6 py-3.5">
        <Link href="#top" className="flex items-center gap-2.5">
          <Brand />
        </Link>

        <nav className="hidden items-center gap-8 md:flex">
          {NAV.map((n) => (
            <a key={n.href} href={n.href} className="text-sm font-semibold text-muted transition-colors hover:text-foreground">
              {n.label}
            </a>
          ))}
        </nav>

        <div className="hidden items-center gap-3 md:flex">
          <ThemeToggle />
          <Link href="/login" className="rounded-xl px-4 py-2.5 text-sm font-bold text-foreground transition-colors hover:bg-surface-muted">
            Giriş Yap
          </Link>
          {showStudioCta && (
            <Link
              href="/register-studio"
              className="rounded-xl bg-primary px-5 py-2.5 text-sm font-bold text-primary-foreground shadow-[0_8px_22px_-10px_var(--primary)] transition-all hover:-translate-y-0.5 hover:bg-primary-hover"
            >
              Stüdyo Hesabı Oluştur
            </Link>
          )}
        </div>

        <button
          aria-label="Menü"
          onClick={() => setOpen((v) => !v)}
          className="flex size-11 items-center justify-center rounded-xl border border-border bg-surface text-foreground md:hidden"
        >
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
            <path d="M4 7h16M4 12h16M4 17h16" />
          </svg>
        </button>
      </div>

      {open && (
        <div className="border-t border-border bg-surface px-6 pb-5 pt-3.5 md:hidden">
          <div className="flex flex-col gap-1">
            {NAV.map((n) => (
              <a
                key={n.href}
                href={n.href}
                onClick={() => setOpen(false)}
                className="rounded-lg px-2 py-3 text-base font-semibold text-foreground"
              >
                {n.label}
              </a>
            ))}
          </div>
          <div className="mt-3.5 flex flex-col gap-2.5">
            <Link href="/login" className="rounded-xl bg-surface-muted py-3 text-center text-sm font-bold text-foreground">
              Giriş Yap
            </Link>
            {showStudioCta && (
              <Link href="/register-studio" className="rounded-xl bg-primary py-3 text-center text-sm font-bold text-primary-foreground">
                Stüdyo Hesabı Oluştur
              </Link>
            )}
          </div>
        </div>
      )}
    </header>
  );
}

/* ------------------------------ Hero ------------------------------ */

function Hero() {
  // Sprint 49: pilotta stüdyo kaydı kapalıysa "Stüdyo Hesabı Oluştur" gizlenir, "Giriş Yap" öne çıkar.
  const showStudioCta = useStudioRegistrationEnabled() !== false;
  return (
    <section id="top" className="relative overflow-hidden px-6 pb-14 pt-16 md:pt-20">
      {/* yumuşak degrade lekeleri */}
      <div className="pointer-events-none absolute -right-20 -top-28 size-[520px] rounded-full bg-[radial-gradient(circle_at_center,var(--primary-soft),transparent_68%)] [animation:pl-drift_16s_ease-in-out_infinite]" />
      <div className="pointer-events-none absolute -left-36 top-28 size-[460px] rounded-full bg-[radial-gradient(circle_at_center,var(--surface-muted),transparent_70%)] [animation:pl-drift2_20s_ease-in-out_infinite]" />

      <div className="relative mx-auto grid max-w-[1160px] items-center gap-10 md:grid-cols-2 md:gap-14">
        <Reveal className="max-w-xl">
          <div className="inline-flex items-center gap-2 rounded-full border border-border bg-surface px-3.5 py-1.5 text-[13px] font-bold text-primary shadow-sm">
            <span className="size-[7px] rounded-full bg-primary" />
            Pilates stüdyoları için yönetim platformu
          </div>
          <h1 className="mt-5 text-4xl font-black leading-[1.06] tracking-tight text-balance sm:text-5xl lg:text-[54px]">
            Pilates stüdyonuzu tek panelden yönetin.
          </h1>
          <p className="mt-5 max-w-lg text-lg leading-relaxed text-muted text-pretty">
            Ders programı, öğrenci paketleri, rezervasyonlar, yoklama, telafi dersleri ve ödemeler — Pliaforma ile stüdyonuzun günlük
            işlerini sade ve düzenli bir akışa taşıyın.
          </p>
          <div className="mt-8 flex flex-wrap gap-3.5">
            {showStudioCta ? (
              <>
                <Link
                  href="/register-studio"
                  className="inline-flex items-center gap-2.5 rounded-2xl bg-primary px-7 py-3.5 text-base font-extrabold text-primary-foreground shadow-[0_14px_30px_-12px_var(--primary)] transition-all hover:-translate-y-0.5 hover:bg-primary-hover"
                >
                  Stüdyo Hesabı Oluştur
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M5 12h14M13 6l6 6-6 6" />
                  </svg>
                </Link>
                <Link
                  href="/login"
                  className="inline-flex items-center rounded-2xl border border-border bg-surface px-6 py-3.5 text-base font-bold text-foreground transition-all hover:-translate-y-0.5 hover:bg-surface-muted"
                >
                  Giriş Yap
                </Link>
              </>
            ) : (
              <Link
                href="/login"
                className="inline-flex items-center gap-2.5 rounded-2xl bg-primary px-7 py-3.5 text-base font-extrabold text-primary-foreground shadow-[0_14px_30px_-12px_var(--primary)] transition-all hover:-translate-y-0.5 hover:bg-primary-hover"
              >
                Giriş Yap
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M5 12h14M13 6l6 6-6 6" />
                </svg>
              </Link>
            )}
          </div>
          <p className="mt-5 flex items-center gap-2 text-[13.5px] text-muted">
            <CheckIcon className="size-4 text-success" />
            {showStudioCta ? "Kurulum gerektirmez · Stüdyonuzu dakikalar içinde tanımlayın" : "Pilot erişim · Mevcut hesabınızla giriş yapın"}
          </p>
        </Reveal>

        <Reveal delay={120}>
          <DashboardMock />
        </Reveal>
      </div>
    </section>
  );
}

function DashboardMock() {
  return (
    <div className="relative">
      {/* yüzen rozetler — küçük ekranda gizli */}
      <div className="absolute -left-3.5 -top-10 z-10 hidden items-center gap-2.5 whitespace-nowrap rounded-2xl border border-border bg-surface px-3.5 py-2.5 shadow-[0_16px_34px_-18px_rgba(61,52,56,0.4)] [animation:pl-float_6s_ease-in-out_infinite] sm:flex">
        <span className="flex size-[34px] items-center justify-center rounded-[10px] bg-primary-soft text-primary">
          <CalendarIcon className="size-[18px]" />
        </span>
        <span>
          <span className="block text-[11px] font-semibold text-muted">Bugün</span>
          <span className="block text-sm font-extrabold text-foreground">8 ders</span>
        </span>
      </div>
      <div className="absolute -right-4 bottom-6 z-10 hidden items-center gap-2.5 whitespace-nowrap rounded-2xl border border-border bg-surface px-3.5 py-2.5 shadow-[0_16px_34px_-18px_rgba(61,52,56,0.4)] [animation:pl-float-slow_7s_ease-in-out_0.8s_infinite] sm:flex">
        <span className="flex size-[34px] items-center justify-center rounded-[10px] bg-success/15 text-success">
          <CheckIcon className="size-[18px]" />
        </span>
        <span>
          <span className="block text-[11px] font-semibold text-muted">Katılım</span>
          <span className="block text-sm font-extrabold text-foreground">%92</span>
        </span>
      </div>

      <div className="relative rounded-3xl border border-border bg-surface p-5 shadow-[0_30px_70px_-30px_rgba(61,52,56,0.42)]">
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center gap-2.5">
            <span className="size-[22px] rounded-[7px] bg-gradient-to-br from-primary to-primary-hover" />
            <span className="text-sm font-extrabold text-foreground">Studio Mira</span>
          </div>
          <span className="rounded-lg bg-surface-muted px-2.5 py-1 text-xs font-semibold text-muted">Haziran 2026</span>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-3">
          <MockStat label="Bugünkü Dersler" value="8" hint="5 reformer · 3 mat" />
          <MockStat label="Rezervasyon" value="37" hint="4 boş kontenjan" />
          <MockStat label="Ders Hakkı Azalan" value="6" hint="≤ 2 ders kaldı" tone="primary" />
          <MockStat label="Ödeme Gerekli" value="3" hint="8.400 ₺ bekliyor" tone="warning" />
        </div>

        <div className="mt-4">
          <div className="mb-2.5 flex items-center justify-between">
            <span className="text-[12.5px] font-extrabold text-foreground">Yaklaşan dersler</span>
            <span className="text-[11px] font-semibold text-primary">Tümü</span>
          </div>
          <div className="flex flex-col gap-2">
            <MockRow dot="bg-primary" name="Reformer Akış" meta="09:00 · Elif Demir" />
            <MockRow dot="bg-success" name="Mat Pilates" meta="10:30 · Selin Yıldız" />
            <MockRow dot="bg-warning" name="Özel Ders" meta="12:00 · Merve Arslan" />
          </div>
        </div>
      </div>
    </div>
  );
}

function MockStat({ label, value, hint, tone }: { label: string; value: string; hint: string; tone?: "primary" | "warning" }) {
  const wrap =
    tone === "primary" ? "bg-primary-soft" : tone === "warning" ? "bg-warning/15" : "bg-surface-muted";
  const labelClr = tone === "primary" ? "text-primary-hover" : tone === "warning" ? "text-warning" : "text-muted";
  const hintClr = tone === "primary" ? "text-primary-hover" : tone === "warning" ? "text-warning" : "text-muted";
  return (
    <div className={cn("rounded-2xl p-3.5", wrap)}>
      <div className={cn("text-[10.5px] font-bold uppercase tracking-wide", labelClr)}>{label}</div>
      <div className="mt-1 text-2xl font-black text-foreground">{value}</div>
      <div className={cn("text-[11px]", hintClr)}>{hint}</div>
    </div>
  );
}

function MockRow({ dot, name, meta }: { dot: string; name: string; meta: string }) {
  return (
    <div className="flex items-center justify-between rounded-xl border border-border bg-surface px-3 py-2.5">
      <div className="flex items-center gap-2.5">
        <span className={cn("size-2 rounded-full", dot)} />
        <span className="text-[12.5px] font-bold text-foreground">{name}</span>
      </div>
      <span className="text-xs text-muted">{meta}</span>
    </div>
  );
}

/* -------------------------- Value cards --------------------------- */

const VALUES = [
  { icon: CalendarIcon, title: "Ders programını düzenle", text: "Seans, kontenjan ve hocaları tek takvimde düzenli tutun." },
  { icon: PackageIcon, title: "Paket ve ödemeleri takip et", text: "Kalan haklar, vadeler ve tahsilatlar tek bakışta görünür." },
  { icon: UsersIcon, title: "Öğrenci ve hocaları tek yerden yönet", text: "Üyelikler, katılım istekleri ve roller düzenli bir akışta." },
];

function ValueCards() {
  return (
    <section className="px-6 pb-5 pt-9">
      <div className="mx-auto grid max-w-[1160px] gap-5 sm:grid-cols-3">
        {VALUES.map((v, i) => (
          <Reveal key={v.title} delay={i * 90}>
            <div className="h-full rounded-2xl border border-border bg-surface p-6 shadow-sm transition-all hover:-translate-y-1 hover:shadow-[0_20px_40px_-24px_rgba(61,52,56,0.34)]">
              <span className="flex size-11 items-center justify-center rounded-xl bg-primary-soft text-primary">
                <v.icon className="size-[22px]" />
              </span>
              <h3 className="mt-4 text-[17px] font-extrabold text-foreground">{v.title}</h3>
              <p className="mt-1.5 text-[14.5px] leading-relaxed text-muted">{v.text}</p>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}

/* ---------------------------- Features ---------------------------- */

const FEATURES = [
  { icon: CalendarIcon, title: "Ders Programı & Rezervasyon", text: "Kontenjan, seans, hoca ve ders grubu bazlı programınızı kolayca yönetin." },
  { icon: PackageIcon, title: "Sözleşme & Paketler", text: "Öğrencinin sözleşme satırlarını (ders tipi, ders sayısı, fiyat, tercih gün/saat) ve kalan ders haklarını takip edin." },
  { icon: TagIcon, title: "Ödeme & Borç Takibi", text: "Öğrenci bazlı kalan ödemeleri ve gecikmiş borçları; her borcun hangi sözleşmeye ait olduğunu tek ekranda görün." },
  { icon: CheckCircleIcon, title: "Yoklama & Katılım", text: "Derse katılan, gelmeyen veya telafi hakkı oluşan öğrencileri takip edin." },
  { icon: RepeatIcon, title: "Telafi Dersleri", text: "Telafi taleplerini oluşturun/onaylayın; iptal, erteleme ve telafi kredilerini düzenli yönetin." },
  { icon: UserIcon, title: "Hoca Yönetimi & İş Hacmi", text: "Hocaları yönetin; aylık planlanan/işlenen ders ve katılım kırılımıyla ödeme hesabınızı kolaylaştırın." },
  { icon: UserPlusIcon, title: "Stüdyoya Katılım İstekleri", text: "Öğrenci veya hoca kendi hesabını oluşturur, katılmak için istek gönderir; stüdyo sahibi onaylar." },
  { icon: BellIcon, title: "Bildirimler", text: "Yaklaşan dersler, ödeme durumları ve önemli aksiyonlar için sistem içi bildirimler." },
];

function Features() {
  return (
    <section id="features" className="scroll-mt-20 px-6 py-[72px]">
      <div className="mx-auto max-w-[1160px]">
        <Reveal className="mx-auto mb-11 max-w-2xl text-center">
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">Özellikler</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight text-balance sm:text-[38px]">Stüdyonuzun her ayrıntısı, tek akışta</h2>
          <p className="mt-3.5 text-[17px] leading-relaxed text-muted">Günlük operasyonu basitleştiren, birbirine bağlı modüller.</p>
        </Reveal>
        <div className="grid gap-[18px] sm:grid-cols-2 lg:grid-cols-4">
          {FEATURES.map((f, i) => (
            <Reveal key={f.title} delay={(i % 4) * 60}>
              <div className="h-full rounded-2xl border border-border bg-surface p-[22px] transition-all hover:-translate-y-1.5 hover:border-primary-soft hover:shadow-[0_22px_44px_-26px_rgba(61,52,56,0.36)]">
                <span className="flex size-[42px] items-center justify-center rounded-xl bg-primary-soft text-primary">
                  <f.icon className="size-[21px]" />
                </span>
                <h3 className="mt-3.5 text-base font-extrabold text-foreground">{f.title}</h3>
                <p className="mt-1.5 text-sm leading-relaxed text-muted">{f.text}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

/* -------------------------- How it works -------------------------- */

const STEPS = [
  { n: "01", title: "Stüdyonuzu oluşturun", text: "Stüdyo sahibi olarak hesabınızı açın ve ilk stüdyo ayarlarınızı tamamlayın." },
  { n: "02", title: "Ders ve paketlerinizi tanımlayın", text: "Ders grupları, hocalar, paket planları ve fiyatları belirleyin." },
  { n: "03", title: "Programı oluşturun ve takip edin", text: "Öğrencileri sözleşmelerine göre derslere ekleyin; yoklama, telafi ve ödemeleri tek panelden yönetin." },
];

function HowItWorks() {
  return (
    <section id="how" className="scroll-mt-16 bg-surface-muted px-6 py-[72px]">
      <div className="mx-auto max-w-[1160px]">
        <Reveal className="mx-auto mb-12 max-w-xl text-center">
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">Nasıl Çalışır</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight sm:text-[38px]">Üç adımda hazır</h2>
        </Reveal>
        <div className="grid gap-[22px] md:grid-cols-3">
          {STEPS.map((s, i) => (
            <Reveal key={s.n} delay={i * 110}>
              <div className="h-full rounded-2xl border border-border bg-surface p-7">
                <div className="flex size-[38px] items-center justify-center rounded-xl bg-primary text-[13px] font-black text-primary-foreground">{s.n}</div>
                <h3 className="mt-[18px] text-lg font-extrabold text-foreground">{s.title}</h3>
                <p className="mt-2 text-[14.5px] leading-relaxed text-muted">{s.text}</p>
              </div>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

/* ---------------------------- Audience ---------------------------- */

const AUDIENCE = [
  "Reformer pilates stüdyoları",
  "Mat pilates grupları",
  "Özel ders veren butik salonlar",
  "Birden fazla hocayla çalışan stüdyolar",
  "Paketli ders sistemi kullanan işletmeler",
];

function Audience() {
  return (
    <section className="px-6 py-[72px]">
      <div className="mx-auto grid max-w-[1160px] items-center gap-12 md:grid-cols-[0.85fr_1.15fr]">
        <Reveal>
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">Kimler için</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight text-balance">Butik stüdyolar için tasarlandı</h2>
          <p className="mt-3.5 text-base leading-relaxed text-muted">
            Küçük ekiplerden çok hocalı stüdyolara — paketli ders sistemini kullanan her işletme için sade bir akış.
          </p>
        </Reveal>
        <Reveal delay={120} className="flex flex-col gap-3">
          {AUDIENCE.map((a) => (
            <div key={a} className="flex items-center gap-3.5 rounded-2xl border border-border bg-surface px-[18px] py-4 transition-transform hover:translate-x-1.5">
              <span className="flex size-9 flex-none items-center justify-center rounded-xl bg-primary-soft text-primary">
                <CheckIcon className="size-[19px]" />
              </span>
              <span className="text-[15.5px] font-bold text-foreground">{a}</span>
            </div>
          ))}
        </Reveal>
      </div>
    </section>
  );
}

/* ---------------------------- Showcase ---------------------------- */

function Showcase() {
  return (
    <section id="showcase" className="scroll-mt-16 bg-surface-muted px-6 py-[72px]">
      <div className="mx-auto max-w-[1160px]">
        <Reveal className="mx-auto mb-11 max-w-xl text-center">
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">Panel</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight text-balance sm:text-[38px]">Günün özetini bir bakışta görün</h2>
          <p className="mt-3.5 text-[17px] leading-relaxed text-muted">Aksiyon gerektiren her şey panelinizde, sade kartlarda.</p>
        </Reveal>
        <div className="grid gap-[18px] sm:grid-cols-2 lg:grid-cols-3">
          <Reveal>
            <ShowcaseCard title="Bugünkü Dersler" badge="8" badgeTone="primary">
              <ListRow a="Reformer Akış" b="09:00" />
              <ListRow a="Mat Pilates" b="10:30" />
              <ListRow a="Reformer Güç" b="18:00" />
            </ShowcaseCard>
          </Reveal>
          <Reveal delay={80}>
            <ShowcaseCard title="Ders Hakkı Azalanlar" badge="6" badgeTone="primary">
              <ListRow a="Deniz Aydın" b="2 ders" bClass="font-bold text-warning" />
              <ListRow a="Ceren Öztürk" b="1 ders" bClass="font-bold text-warning" />
              <ListRow a="Burak Şahin" b="2 ders" bClass="font-bold text-warning" />
            </ShowcaseCard>
          </Reveal>
          <Reveal delay={160}>
            <ShowcaseCard title="Ödeme Gerekli" badge="3" badgeTone="danger">
              <ListRow a="Ece Yılmaz" b="2.800 ₺" bClass="font-bold text-foreground" />
              <ListRow a="Mert Koç" b="3.200 ₺" bClass="font-bold text-foreground" />
              <ListRow a="Naz Eren" b="2.400 ₺" bClass="font-bold text-foreground" />
            </ShowcaseCard>
          </Reveal>
          <Reveal>
            <ShowcaseCard title="Bekleyen Katılım İstekleri" badge="2" badgeTone="primary">
              <RequestRow name="Ayşe Çelik" role="Hoca · başvuru" />
              <RequestRow name="Kerem Acar" role="Öğrenci · başvuru" />
            </ShowcaseCard>
          </Reveal>
          <Reveal delay={80} className="sm:col-span-2">
            <div className="h-full rounded-2xl border border-border bg-surface p-5 shadow-sm">
              <div className="flex items-center justify-between">
                <span className="text-[13px] font-extrabold text-foreground">Yaklaşan Rezervasyonlar</span>
                <span className="rounded-md bg-surface-muted px-2.5 py-0.5 text-[11px] font-bold text-muted">Bu hafta</span>
              </div>
              <div className="mt-3.5 grid gap-2.5 sm:grid-cols-2">
                <RezRow dot="bg-primary" name="Reformer Akış" when="Pzt 09:00" />
                <RezRow dot="bg-success" name="Mat Pilates" when="Salı 10:30" />
                <RezRow dot="bg-warning" name="Özel Ders" when="Çar 12:00" />
                <RezRow dot="bg-primary" name="Reformer Güç" when="Per 18:00" />
              </div>
            </div>
          </Reveal>
        </div>
      </div>
    </section>
  );
}

function ShowcaseCard({
  title,
  badge,
  badgeTone,
  children,
}: {
  title: string;
  badge: string;
  badgeTone: "primary" | "danger";
  children: React.ReactNode;
}) {
  const tone = badgeTone === "danger" ? "bg-danger/12 text-danger" : "bg-primary-soft text-primary";
  return (
    <div className="h-full rounded-2xl border border-border bg-surface p-5 shadow-sm">
      <div className="flex items-center justify-between">
        <span className="text-[13px] font-extrabold text-foreground">{title}</span>
        <span className={cn("rounded-md px-2.5 py-0.5 text-[11px] font-bold", tone)}>{badge}</span>
      </div>
      <div className="mt-3.5 flex flex-col gap-2.5">{children}</div>
    </div>
  );
}

function ListRow({ a, b, bClass }: { a: string; b: string; bClass?: string }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-[13.5px] font-semibold text-foreground">{a}</span>
      <span className={cn("text-[12.5px] text-muted", bClass)}>{b}</span>
    </div>
  );
}

function RequestRow({ name, role }: { name: string; role: string }) {
  return (
    <div className="flex items-center justify-between">
      <div>
        <div className="text-[13.5px] font-semibold text-foreground">{name}</div>
        <div className="text-[11.5px] text-muted">{role}</div>
      </div>
      <span className="rounded-md bg-success/15 px-2.5 py-0.5 text-[11px] font-bold text-success">Onayla</span>
    </div>
  );
}

function RezRow({ dot, name, when }: { dot: string; name: string; when: string }) {
  return (
    <div className="flex items-center justify-between rounded-xl bg-surface-muted px-3.5 py-2.5">
      <div className="flex items-center gap-2.5">
        <span className={cn("size-[7px] rounded-full", dot)} />
        <span className="text-[13px] font-semibold text-foreground">{name}</span>
      </div>
      <span className="text-xs text-muted">{when}</span>
    </div>
  );
}

/* ------------------------------- FAQ ------------------------------ */

const FAQ = [
  { q: "Pliaforma kimler için?", a: "Paketli ders sistemiyle çalışan reformer ve mat pilates stüdyoları, butik salonlar ve birden fazla hocayla çalışan işletmeler için tasarlandı." },
  { q: "Öğrenci ve hoca kendi kaydolabilir mi?", a: "Evet. Öğrenci veya hoca kendi hesabını oluşturur ve stüdyoya katılmak için istek gönderir; stüdyo sahibi bu isteği onaylayarak üyeliği başlatır." },
  { q: "Paket hakları nasıl takip edilir?", a: "Her öğrencinin aktif paketleri, kalan ders hakları ve hangi ders gruplarında geçerli olduğu panelde görünür. Rezervasyon yapıldıkça haklar otomatik düşer." },
  { q: "Fiyat değişince eski öğrenciler etkilenir mi?", a: "Yeni fiyatlar belirlediğiniz tarihten itibaren geçerli olur. Mevcut öğrencilerin satın aldığı paket hakları korunur; değişiklik geriye dönük uygulanmaz." },
  { q: "Telafi dersleri yönetilebilir mi?", a: "Evet. İptal ve ertelemelerden oluşan telafi kredilerini tanımlar, öğrencilere atar ve kullanım durumlarını panelden takip edersiniz." },
  { q: "Giriş yaptıktan sonra ne olur?", a: "Doğrudan stüdyo panelinize yönlendirilirsiniz. Rolünüze göre (stüdyo sahibi, hoca veya öğrenci) size özel dashboard ve günlük işleriniz açılır." },
];

function Faq() {
  const [open, setOpen] = useState<number | null>(0);
  return (
    <section id="faq" className="scroll-mt-16 px-6 py-[72px]">
      <div className="mx-auto max-w-[780px]">
        <Reveal className="mb-10 text-center">
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">SSS</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight sm:text-[38px]">Sık sorulan sorular</h2>
        </Reveal>
        <Reveal className="flex flex-col gap-3">
          {FAQ.map((item, i) => {
            const isOpen = open === i;
            return (
              <div key={item.q} className={cn("overflow-hidden rounded-2xl border bg-surface transition-colors", isOpen ? "border-primary-soft" : "border-border")}>
                <button
                  onClick={() => setOpen(isOpen ? null : i)}
                  className="flex w-full items-center justify-between gap-4 px-[22px] py-5 text-left text-[16.5px] font-extrabold text-foreground"
                >
                  {item.q}
                  <svg
                    className={cn("size-5 flex-none text-primary transition-transform duration-300", isOpen && "rotate-180")}
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2.2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <path d="M6 9l6 6 6-6" />
                  </svg>
                </button>
                <div className={cn("grid transition-all duration-300", isOpen ? "grid-rows-[1fr]" : "grid-rows-[0fr]")}>
                  <div className="overflow-hidden">
                    <p className="px-[22px] pb-5 text-[15px] leading-relaxed text-muted">{item.a}</p>
                  </div>
                </div>
              </div>
            );
          })}
        </Reveal>
      </div>
    </section>
  );
}

/* ---------------------------- Final CTA --------------------------- */

function FinalCta() {
  // Sprint 49: pilotta stüdyo kaydı kapalıysa yalnız "Giriş Yap".
  const showStudioCta = useStudioRegistrationEnabled() !== false;
  return (
    <section className="px-6 pb-20 pt-9">
      <Reveal className="mx-auto max-w-[1000px]">
        <div className="relative overflow-hidden rounded-[28px] bg-gradient-to-br from-primary to-primary-hover px-6 py-14 text-center shadow-[0_30px_70px_-34px_var(--primary)] sm:px-10 sm:py-16">
          <div className="pointer-events-none absolute -right-10 -top-20 size-72 rounded-full bg-white/10" />
          <div className="pointer-events-none absolute -bottom-24 -left-10 size-72 rounded-full bg-white/[0.08]" />
          <h2 className="relative text-3xl font-black tracking-tight text-balance text-white sm:text-[40px]">
            Stüdyonuzu daha düzenli yönetmeye başlayın.
          </h2>
          <p className="relative mx-auto mt-3.5 max-w-lg text-[17px] leading-relaxed text-white/90">
            Pliaforma ile günlük işleriniz tek panelde, sade bir akışta toplansın.
          </p>
          <div className="relative mt-7 flex flex-wrap justify-center gap-3.5">
            {showStudioCta ? (
              <>
                <Link href="/register-studio" className="rounded-2xl bg-white px-7 py-3.5 text-base font-extrabold text-primary shadow-[0_14px_30px_-14px_rgba(0,0,0,0.4)] transition-transform hover:-translate-y-0.5">
                  Stüdyo Hesabı Oluştur
                </Link>
                <Link href="/login" className="rounded-2xl border border-white/40 bg-white/15 px-7 py-3.5 text-base font-bold text-white transition-colors hover:bg-white/25">
                  Giriş Yap
                </Link>
              </>
            ) : (
              <Link href="/login" className="rounded-2xl bg-white px-7 py-3.5 text-base font-extrabold text-primary shadow-[0_14px_30px_-14px_rgba(0,0,0,0.4)] transition-transform hover:-translate-y-0.5">
                Giriş Yap
              </Link>
            )}
          </div>
        </div>
      </Reveal>
    </section>
  );
}

/* ----------------------------- Footer ----------------------------- */

function Footer() {
  // Sprint 49: pilotta kayıt kapalıysa "Başla" sütununda yalnız Giriş Yap kalır.
  const showStudioCta = useStudioRegistrationEnabled() !== false;
  const startLinks = showStudioCta
    ? [
        { href: "/register-studio", label: "Stüdyo Hesabı Oluştur" },
        { href: "/login", label: "Giriş Yap" },
        { href: "/register", label: "Öğrenci / Hoca Hesabı" },
      ]
    : [{ href: "/login", label: "Giriş Yap" }];
  return (
    <footer className="border-t border-border px-6 pb-9 pt-[52px]">
      <div className="mx-auto grid max-w-[1160px] gap-9 sm:grid-cols-2 md:grid-cols-[1.6fr_1fr_1fr_1fr]">
        <div>
          <div className="flex items-center gap-2.5">
            <PliaformaMark className="size-[34px]" />
            <span className="text-lg font-extrabold tracking-tight text-foreground">
              Plia<span className="text-primary">forma</span>
            </span>
          </div>
          <p className="mt-3.5 max-w-[300px] text-[14.5px] leading-relaxed text-muted">
            Pilates stüdyoları için sade, modern ve premium yönetim platformu. Ders, paket ve ödemeler tek panelde.
          </p>
        </div>
        <FooterCol title="Başla" links={startLinks} />
        <FooterCol title="Ürün" links={[
          { href: "#features", label: "Özellikler" },
          { href: "#how", label: "Nasıl Çalışır" },
          { href: "#faq", label: "SSS" },
        ]} />
        <FooterCol title="Destek" links={[
          { href: "#", label: "İletişim" },
          { href: "#", label: "Yardım Merkezi" },
        ]} />
      </div>
      <div className="mx-auto mt-9 flex max-w-[1160px] flex-wrap items-center justify-between gap-4 border-t border-border pt-[22px]">
        <span className="text-[13px] text-muted">© 2026 Pliaforma. Tüm hakları saklıdır.</span>
        <span className="text-[13px] text-muted">Pilates stüdyoları için ❤ ile</span>
      </div>
    </footer>
  );
}

function FooterCol({ title, links }: { title: string; links: { href: string; label: string }[] }) {
  return (
    <div>
      <div className="text-xs font-extrabold uppercase tracking-[0.08em] text-muted">{title}</div>
      <div className="mt-3.5 flex flex-col gap-2.5">
        {links.map((l) =>
          l.href === "#" ? (
            // Henüz gerçek bir hedefi olmayan öğeler link gibi gösterilmez (yanıltıcı ölü link olmasın).
            <span key={l.label} className="text-[14.5px] font-semibold text-muted">{l.label}</span>
          ) : l.href.startsWith("#") ? (
            <a key={l.label} href={l.href} className="text-[14.5px] font-semibold text-foreground transition-colors hover:text-primary">{l.label}</a>
          ) : (
            <Link key={l.label} href={l.href} className="text-[14.5px] font-semibold text-foreground transition-colors hover:text-primary">{l.label}</Link>
          ),
        )}
      </div>
    </div>
  );
}

/* ------------------------- Reveal helper -------------------------- */

function Reveal({ children, delay = 0, className }: { children: React.ReactNode; delay?: number; className?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const [shown, setShown] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(
      ([e]) => {
        if (e.isIntersecting) {
          setShown(true);
          io.disconnect();
        }
      },
      { threshold: 0.12, rootMargin: "0px 0px -8% 0px" },
    );
    io.observe(el);
    return () => io.disconnect();
  }, []);
  return (
    <div
      ref={ref}
      style={{ transitionDelay: `${delay}ms` }}
      className={cn("transition-all duration-700 ease-out", shown ? "translate-y-0 opacity-100" : "translate-y-6 opacity-0", className)}
    >
      {children}
    </div>
  );
}

/* --------------------------- Keyframes ---------------------------- */

function LandingKeyframes() {
  return (
    <style>{`
      @keyframes pl-float{0%,100%{transform:translateY(0)}50%{transform:translateY(-9px)}}
      @keyframes pl-float-slow{0%,100%{transform:translateY(0)}50%{transform:translateY(-6px)}}
      @keyframes pl-drift{0%{transform:translate(0,0) scale(1)}50%{transform:translate(24px,-20px) scale(1.08)}100%{transform:translate(0,0) scale(1)}}
      @keyframes pl-drift2{0%{transform:translate(0,0) scale(1)}50%{transform:translate(-26px,18px) scale(1.06)}100%{transform:translate(0,0) scale(1)}}
    `}</style>
  );
}

/* ----------------------------- Icons ------------------------------ */

type IconProps = { className?: string };
const I = (props: React.SVGProps<SVGSVGElement>) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" {...props} />
);

function CalendarIcon({ className }: IconProps) {
  return <I className={className}><rect x="3" y="4.5" width="18" height="16" rx="2.5" /><path d="M3 9h18M8 3v3M16 3v3" /></I>;
}
function PackageIcon({ className }: IconProps) {
  return <I className={className}><path d="M21 8 12 3 3 8v8l9 5 9-5z" /><path d="M3 8l9 5 9-5M12 13v8" /></I>;
}
function TagIcon({ className }: IconProps) {
  return <I className={className}><path d="M12.5 3H4a1 1 0 0 0-1 1v8.4L12 21l9-9-8.5-9z" /><circle cx="7.6" cy="7.6" r="1.5" /></I>;
}
function CheckCircleIcon({ className }: IconProps) {
  return <I className={className}><circle cx="12" cy="12" r="9" /><path d="M8.5 12.5l2.5 2.5 4.5-5" /></I>;
}
function RepeatIcon({ className }: IconProps) {
  return <I className={className}><path d="M17 2l3 3-3 3" /><path d="M20 5H8a4 4 0 0 0-4 4v1" /><path d="M7 22l-3-3 3-3" /><path d="M4 19h12a4 4 0 0 0 4-4v-1" /></I>;
}
function UserIcon({ className }: IconProps) {
  return <I className={className}><circle cx="12" cy="8" r="3.6" /><path d="M5 20c0-3.3 3-5.5 7-5.5s7 2.2 7 5.5" /></I>;
}
function UserPlusIcon({ className }: IconProps) {
  return <I className={className}><circle cx="10" cy="8" r="3.4" /><path d="M3 20c0-3.3 2.8-5.5 7-5.5" /><path d="M17 9v6M14 12h6" /></I>;
}
function UsersIcon({ className }: IconProps) {
  return <I className={className}><circle cx="9" cy="8" r="3.2" /><path d="M3 19c0-3 2.7-5 6-5s6 2 6 5" /><circle cx="17.5" cy="9" r="2.6" /><path d="M16 14.4c2.6.2 4.5 2 4.5 4.6" /></I>;
}
function BellIcon({ className }: IconProps) {
  return <I className={className}><path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8" /><path d="M10.5 21a2 2 0 0 0 3 0" /></I>;
}
function CheckIcon({ className }: IconProps) {
  return <I className={className} strokeWidth="2.2"><path d="M20 6 9 17l-5-5" /></I>;
}
