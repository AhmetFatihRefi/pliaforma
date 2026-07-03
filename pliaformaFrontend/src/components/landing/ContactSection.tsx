"use client";

import { useState } from "react";
import { api, ApiError } from "@/lib/api";
import { cn } from "@/lib/cn";

/* ------------------------------------------------------------------ */
/*  Sprint 52 — İletişim / demo talep formu (landing #contact)         */
/*  POST /api/contact → backend Resend/SMTP ile PRIVATE alıcıya mail   */
/*  atar (CONTACT_TO_EMAIL). Kişisel e-posta landing'de GÖRÜNMEZ.      */
/*  DB kaydı yok; "website" alanı honeypot'tur (insan görmez).         */
/* ------------------------------------------------------------------ */

// Backend'deki üst sınırlarla aynı (ContactController.Validate).
const MAX = { fullName: 100, studioName: 150, email: 200, phone: 40, city: 100, message: 2000 } as const;

type Status = "idle" | "submitting" | "success" | "error";

export function ContactSection() {
  const [status, setStatus] = useState<Status>("idle");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const onSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const form = e.currentTarget;
    const data = new FormData(form);
    const value = (k: string) => String(data.get(k) ?? "").trim();

    const body = {
      fullName: value("fullName"),
      studioName: value("studioName"),
      email: value("email"),
      phone: value("phone"),
      city: value("city"),
      message: value("message"),
      website: value("website"), // honeypot — insan kullanıcıda daima boş
    };

    // Client-side ön doğrulama (backend aynı kuralları ayrıca uygular).
    const errors: Record<string, string> = {};
    if (body.fullName.length < 2) errors.fullName = "Ad soyad gerekli.";
    if (body.studioName.length < 2) errors.studioName = "Stüdyo adı gerekli.";
    if (!body.email && !body.phone) errors.email = "E-posta veya telefondan en az birini girin.";
    if (body.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(body.email)) errors.email = "Geçerli bir e-posta girin.";
    if (body.message.length < 10) errors.message = "Mesajınız en az 10 karakter olmalı.";
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setStatus("submitting");
    setErrorMsg(null);
    try {
      await api<null>("/api/contact", { method: "POST", body, skipAuthRetry: true });
      setStatus("success");
    } catch (err) {
      // Teknik detay göstermeyiz; backend mesajları zaten sade Türkçedir.
      setErrorMsg(err instanceof ApiError && err.status === 429
        ? "Çok fazla deneme yapıldı. Lütfen bir süre sonra tekrar deneyin."
        : "Mesajınız gönderilemedi. Lütfen daha sonra tekrar deneyin.");
      setStatus("error");
    }
  };

  return (
    <section id="contact" className="scroll-mt-16 bg-surface-muted px-6 py-[72px]">
      <div className="mx-auto grid max-w-[1060px] items-start gap-10 md:grid-cols-[0.9fr_1.1fr] md:gap-14">
        <div>
          <div className="text-[13px] font-extrabold uppercase tracking-[0.1em] text-primary">İletişim</div>
          <h2 className="mt-3 text-3xl font-black tracking-tight text-balance sm:text-[38px]">Demo talep edin</h2>
          <p className="mt-3.5 text-base leading-relaxed text-muted">
            Pliaforma şu anda pilot aşamada. Stüdyonuz için uygunluğu birlikte değerlendirmek üzere formu doldurun;
            size en kısa sürede dönüş yapalım.
          </p>
          <ul className="mt-6 flex flex-col gap-3 text-[14.5px] text-muted">
            <li className="flex items-center gap-2.5">
              <span className="size-[7px] rounded-full bg-primary" /> Kuruluma özel yönlendirme
            </li>
            <li className="flex items-center gap-2.5">
              <span className="size-[7px] rounded-full bg-primary" /> Stüdyonuza uygunluk değerlendirmesi
            </li>
            <li className="flex items-center gap-2.5">
              <span className="size-[7px] rounded-full bg-primary" /> Pilot erişim planlaması
            </li>
          </ul>
        </div>

        {status === "success" ? (
          <div className="rounded-3xl border border-border bg-surface p-8 text-center shadow-sm">
            <span className="mx-auto flex size-12 items-center justify-center rounded-full bg-success/15 text-success">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 6 9 17l-5-5" />
              </svg>
            </span>
            <h3 className="mt-4 text-xl font-extrabold text-foreground">Mesajınız alındı</h3>
            <p className="mt-2 text-[15px] leading-relaxed text-muted">En kısa sürede size dönüş yapacağız.</p>
          </div>
        ) : (
          <form onSubmit={onSubmit} noValidate className="relative rounded-3xl border border-border bg-surface p-6 shadow-sm sm:p-8">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Ad Soyad" name="fullName" required maxLength={MAX.fullName} error={fieldErrors.fullName} autoComplete="name" />
              <Field label="Stüdyo Adı" name="studioName" required maxLength={MAX.studioName} error={fieldErrors.studioName} autoComplete="organization" />
              <Field label="E-posta" name="email" type="email" maxLength={MAX.email} error={fieldErrors.email} autoComplete="email" hint="E-posta veya telefondan en az biri" />
              <Field label="Telefon" name="phone" type="tel" maxLength={MAX.phone} error={fieldErrors.phone} autoComplete="tel" />
              <Field label="Şehir (opsiyonel)" name="city" maxLength={MAX.city} error={fieldErrors.city} className="sm:col-span-2" autoComplete="address-level2" />
            </div>

            <label className="mt-4 block">
              <span className="text-[13px] font-bold text-foreground">Mesajınız</span>
              <textarea
                name="message"
                required
                rows={4}
                maxLength={MAX.message}
                placeholder="Stüdyonuzdan ve ihtiyaçlarınızdan kısaca bahsedin…"
                className={cn(
                  "mt-1.5 w-full rounded-xl border bg-background px-3.5 py-2.5 text-sm text-foreground placeholder:text-muted focus:border-primary focus:outline-none",
                  fieldErrors.message ? "border-danger" : "border-border",
                )}
              />
              {fieldErrors.message && <span className="mt-1 block text-xs text-danger">{fieldErrors.message}</span>}
            </label>

            {/* Honeypot: görsel olarak gizli; botlar doldurur, backend dolu gelirse sessizce yok sayar. */}
            <div aria-hidden="true" className="absolute -left-[9999px] top-auto h-px w-px overflow-hidden">
              <label>
                Web siteniz
                <input type="text" name="website" tabIndex={-1} autoComplete="off" />
              </label>
            </div>

            {status === "error" && errorMsg && (
              <p className="mt-4 rounded-xl bg-danger/10 px-4 py-3 text-sm font-semibold text-danger">{errorMsg}</p>
            )}

            <button
              type="submit"
              disabled={status === "submitting"}
              className="mt-5 w-full rounded-2xl bg-primary px-7 py-3.5 text-base font-extrabold text-primary-foreground shadow-[0_14px_30px_-12px_var(--primary)] transition-all hover:-translate-y-0.5 hover:bg-primary-hover disabled:cursor-not-allowed disabled:opacity-60"
            >
              {status === "submitting" ? "Gönderiliyor…" : "Demo Talep Et"}
            </button>
            <p className="mt-3 text-center text-xs text-muted">
              Bilgileriniz yalnızca size dönüş yapmak için kullanılır.
            </p>
          </form>
        )}
      </div>
    </section>
  );
}

function Field({
  label,
  name,
  type = "text",
  required,
  maxLength,
  error,
  hint,
  className,
  autoComplete,
}: {
  label: string;
  name: string;
  type?: string;
  required?: boolean;
  maxLength: number;
  error?: string;
  hint?: string;
  className?: string;
  autoComplete?: string;
}) {
  return (
    <label className={cn("block", className)}>
      <span className="text-[13px] font-bold text-foreground">{label}</span>
      <input
        type={type}
        name={name}
        required={required}
        maxLength={maxLength}
        autoComplete={autoComplete}
        className={cn(
          "mt-1.5 w-full rounded-xl border bg-background px-3.5 py-2.5 text-sm text-foreground placeholder:text-muted focus:border-primary focus:outline-none",
          error ? "border-danger" : "border-border",
        )}
      />
      {error ? (
        <span className="mt-1 block text-xs text-danger">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-xs text-muted">{hint}</span>
      ) : null}
    </label>
  );
}
