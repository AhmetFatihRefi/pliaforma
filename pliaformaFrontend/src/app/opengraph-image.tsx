import { ImageResponse } from "next/og";

// Sprint 52 — OpenGraph/Twitter paylaşım görseli (1200x630). Harici asset/font YOK; marka renkleriyle
// runtime'da üretilir. twitter-image dosyası ayrıca yoktur — Next bu görseli Twitter card için de kullanır.
export const alt = "Pliaforma — Pilates Stüdyo Yönetim Uygulaması";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          background: "linear-gradient(135deg, #faf6f2 0%, #f7e7e9 100%)",
          color: "#3d3438",
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 24,
          }}
        >
          <div
            style={{
              width: 72,
              height: 72,
              borderRadius: 20,
              background: "linear-gradient(135deg, #c97c8a 0%, #a95a69 100%)",
            }}
          />
          <div style={{ display: "flex", fontSize: 88, fontWeight: 800, letterSpacing: -2 }}>
            <span>Plia</span>
            <span style={{ color: "#c97c8a" }}>forma</span>
          </div>
        </div>
        <div
          style={{
            marginTop: 28,
            fontSize: 34,
            color: "#8a7d81",
            textAlign: "center",
            maxWidth: 900,
          }}
        >
          Pilates stüdyonuzu tek panelden yönetin
        </div>
        <div
          style={{
            marginTop: 18,
            fontSize: 24,
            color: "#a95a69",
          }}
        >
          Ders programı · Öğrenci takibi · Telafi · Ödemeler
        </div>
      </div>
    ),
    size,
  );
}
