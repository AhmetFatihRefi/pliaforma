"use client";

import Link from "next/link";
import { Button, Card } from "@/components/ui";

/**
 * Sprint 18b: öğrenci self-booking KALDIRILDI — ders programı stüdyo tarafından oluşturulur.
 * Bu sayfa artık rezervasyon yapmaz; yalnızca bilgilendirir ve öğrenciyi "Programım" ekranına yönlendirir.
 * (Doğrudan URL'ye gelinirse boş/404 yerine bu bilgi ekranı gösterilir.)
 */
export default function BookClassesPage() {
  return (
    <div className="mx-auto max-w-xl py-10">
      <Card>
        <h1 className="mb-2 text-xl font-semibold text-foreground">Ders rezervasyonu</h1>
        <p className="mb-4 text-sm text-muted">
          Ders programınız <strong>stüdyo tarafından oluşturulur</strong>. Artık kendiniz ders seçip rezervasyon
          yapmıyorsunuz. Planlanan derslerinizi <strong>“Programım”</strong> ekranından takip edebilirsiniz.
        </p>
        <Link href="/dashboard/my-bookings">
          <Button>Programıma git</Button>
        </Link>
      </Card>
    </div>
  );
}
