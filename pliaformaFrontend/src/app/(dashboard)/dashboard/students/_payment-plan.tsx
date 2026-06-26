"use client";

// Sprint 38 — Ödeme planı (taksit/vade) editörü + yardımcılar. contracts/new ve ContractDetailModal ortak kullanır.
// Peşin = tek taksit (Amount = toplam). Taksitli = otomatik aylık | özel tarihler. Σ amount == toplam ücret.
import { useEffect, useMemo, useState } from "react";
import { Badge, Button, Input, Select } from "@/components/ui";
import type { InstallmentInput } from "@/lib/domain-types";

const round2 = (n: number) => Math.round((Number.isFinite(n) ? n : 0) * 100) / 100;
export const planMoney = (n: number, cur: string) =>
  `${n.toLocaleString("tr-TR", { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${cur}`;
export const sumAmounts = (xs: InstallmentInput[]) => round2(xs.reduce((s, i) => s + (Number(i.amount) || 0), 0));
export const planMatches = (xs: InstallmentInput[], total: number) => Math.round(sumAmounts(xs) * 100) === Math.round(round2(total) * 100);

export const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

// Toplamı n taksite eşit böl; kalan kuruş farkı son taksite (Σ her zaman total).
export function splitEqual(total: number, n: number): number[] {
  const cents = Math.round(round2(total) * 100);
  if (n <= 1) return [round2(total)];
  const base = Math.floor(cents / n);
  const amts = Array<number>(n).fill(base);
  amts[n - 1] += cents - base * n;
  return amts.map((c) => c / 100);
}

// iso tarihine k ay ekle; aynı gün mümkünse aynı gün, değilse ayın son günü (31 Oca + 1 ay → 28/29 Şub).
export function addMonthsSameDay(iso: string, k: number): string {
  const [y, m, d] = iso.split("-").map(Number);
  const idx = m - 1 + k;
  const year = y + Math.floor(idx / 12);
  const month = ((idx % 12) + 12) % 12; // 0..11
  const lastDay = new Date(year, month + 1, 0).getDate();
  const day = Math.min(d, lastDay);
  return `${year}-${String(month + 1).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

export function buildAutoInstallments(total: number, count: number, firstDue: string): InstallmentInput[] {
  const n = Math.max(1, Math.min(60, Math.floor(count) || 1));
  const amts = splitEqual(total, n);
  return amts.map((amount, i) => ({ dueDate: addMonthsSameDay(firstDue, i), amount, notes: null }));
}

type Mode = "cash" | "installment";
type Method = "auto" | "custom";

export function PaymentPlanEditor({
  total, currency, value, onChange, disabled,
}: {
  total: number;
  currency: string;
  value: InstallmentInput[];
  onChange: (installments: InstallmentInput[]) => void;
  disabled?: boolean;
}) {
  const [mode, setMode] = useState<Mode>(value.length >= 2 ? "installment" : "cash");
  const [method, setMethod] = useState<Method>(value.length >= 2 ? "custom" : "auto");
  const [cashDate, setCashDate] = useState<string>(value[0]?.dueDate ?? todayIso());
  const [autoCount, setAutoCount] = useState<number>(value.length >= 2 ? value.length : 3);
  const [autoFirst, setAutoFirst] = useState<string>(value[0]?.dueDate ?? todayIso());
  const [customRows, setCustomRows] = useState<InstallmentInput[]>(
    value.length >= 2 ? value.map((v) => ({ dueDate: v.dueDate, amount: v.amount, notes: v.notes ?? null })) : buildAutoInstallments(total, 3, todayIso()),
  );

  // Hesaplanan installment listesi.
  const installments = useMemo<InstallmentInput[]>(() => {
    if (mode === "cash") return [{ dueDate: cashDate, amount: round2(total), notes: null }];
    if (method === "auto") return buildAutoInstallments(total, autoCount, autoFirst);
    return customRows.map((r) => ({ dueDate: r.dueDate, amount: round2(Number(r.amount) || 0), notes: r.notes ?? null }));
  }, [mode, method, cashDate, total, autoCount, autoFirst, customRows]);

  // Üst bileşene yansıt (JSON-stable → döngü yok).
  const serialized = JSON.stringify(installments);
  useEffect(() => { onChange(installments); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [serialized]);

  const sum = sumAmounts(installments);
  const matches = planMatches(installments, total);

  const setRow = (i: number, patch: Partial<InstallmentInput>) =>
    setCustomRows((rs) => rs.map((r, j) => (j === i ? { ...r, ...patch } : r)));

  return (
    <div className="space-y-3">
      {/* Ödeme tipi */}
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" variant={mode === "cash" ? "primary" : "secondary"} disabled={disabled} onClick={() => setMode("cash")}>Peşin</Button>
        <Button type="button" size="sm" variant={mode === "installment" ? "primary" : "secondary"} disabled={disabled} onClick={() => setMode("installment")}>Taksitli</Button>
      </div>

      {mode === "cash" ? (
        <div className="flex flex-wrap items-end gap-2">
          <label className="text-xs text-muted">
            <div className="mb-1">Ödeme tarihi</div>
            <Input type="date" value={cashDate} disabled={disabled} onChange={(e) => setCashDate(e.target.value)} className="w-44" />
          </label>
          <Badge tone="neutral">Tutar: {planMoney(round2(total), currency)}</Badge>
        </div>
      ) : (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" variant={method === "auto" ? "primary" : "secondary"} disabled={disabled} onClick={() => setMethod("auto")}>Otomatik aylık</Button>
            <Button type="button" size="sm" variant={method === "custom" ? "primary" : "secondary"} disabled={disabled} onClick={() => { setMethod("custom"); setCustomRows(buildAutoInstallments(total, autoCount, autoFirst)); }}>Özel tarihler</Button>
          </div>

          {method === "auto" ? (
            <div className="flex flex-wrap items-end gap-3">
              <label className="text-xs text-muted">
                <div className="mb-1">Taksit sayısı</div>
                <Input type="number" min={2} max={60} value={autoCount} disabled={disabled} onChange={(e) => setAutoCount(Number(e.target.value))} className="w-24" />
              </label>
              <label className="text-xs text-muted">
                <div className="mb-1">İlk vade</div>
                <Input type="date" value={autoFirst} disabled={disabled} onChange={(e) => setAutoFirst(e.target.value)} className="w-44" />
              </label>
              <p className="text-xs text-muted">Her ay aynı gün; kısa ayda ayın son günü. Yuvarlama farkı son taksite.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {customRows.map((r, i) => (
                <div key={i} className="flex flex-wrap items-end gap-2">
                  <Input type="date" value={r.dueDate} disabled={disabled} onChange={(e) => setRow(i, { dueDate: e.target.value })} className="w-44" />
                  <Input type="number" step="0.01" min={0} value={r.amount} disabled={disabled} onChange={(e) => setRow(i, { amount: Number(e.target.value) })} className="w-32" />
                  <span className="text-xs text-muted">{currency}</span>
                  <Button type="button" size="sm" variant="ghost" disabled={disabled || customRows.length <= 1} onClick={() => setCustomRows((rs) => rs.filter((_, j) => j !== i))}>Sil</Button>
                </div>
              ))}
              <Button type="button" size="sm" variant="secondary" disabled={disabled} onClick={() => setCustomRows((rs) => [...rs, { dueDate: todayIso(), amount: 0, notes: null }])}>+ Taksit ekle</Button>
            </div>
          )}
        </div>
      )}

      {/* Önizleme */}
      <div className="rounded-xl border border-border p-2.5">
        <div className="mb-1 flex items-center justify-between text-xs">
          <span className="font-medium text-foreground">Önizleme ({installments.length} taksit)</span>
          <span className={matches ? "text-success" : "text-danger"}>
            Toplam {planMoney(sum, currency)} / olması gereken {planMoney(round2(total), currency)}
          </span>
        </div>
        <ul className="space-y-0.5 text-xs text-muted">
          {installments.map((i, idx) => (
            <li key={idx} className="flex justify-between"><span>{idx + 1}. {i.dueDate}</span><span className="text-foreground">{planMoney(round2(i.amount), currency)}</span></li>
          ))}
        </ul>
        {!matches && <p className="mt-1 text-xs text-danger">Taksit toplamı sözleşme tutarına eşit olmalı.</p>}
      </div>
    </div>
  );
}
