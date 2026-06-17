"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Sidebar } from "@/components/layout/Sidebar";
import { Header } from "@/components/layout/Header";
import { LicenseBanner } from "@/components/layout/LicenseBanner";
import { FullscreenLoader } from "@/components/layout/FullscreenLoader";

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, activeStudio, memberships, loading } = useAuth();
  const router = useRouter();

  // Stüdyo bağlamı olmayan PlatformAdmin'i platform paneline yönlendir (tenant dashboard'u onun için anlamsız).
  const platformOnly = user?.globalRole === "PlatformAdmin" && !activeStudio;
  // Aktif stüdyo üyeliği olmayan normal kullanıcı → katılım ekranı (Sprint 15).
  const needsJoin = !!user && !platformOnly && !activeStudio && memberships.length === 0;

  useEffect(() => {
    if (loading) return;
    if (!user) router.replace("/login");
    else if (platformOnly) router.replace("/platform");
    else if (needsJoin) router.replace("/join-studio");
  }, [loading, user, platformOnly, needsJoin, router]);

  if (loading || !user || platformOnly || needsJoin) return <FullscreenLoader />;

  return (
    <div className="flex min-h-dvh bg-background">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <Header />
        <LicenseBanner />
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
