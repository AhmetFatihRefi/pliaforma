export function FullscreenLoader({ label = "Yükleniyor…" }: { label?: string }) {
  return (
    <div className="flex min-h-dvh items-center justify-center bg-background">
      <div className="flex flex-col items-center gap-3 text-muted">
        <span className="size-7 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <span className="text-sm">{label}</span>
      </div>
    </div>
  );
}
