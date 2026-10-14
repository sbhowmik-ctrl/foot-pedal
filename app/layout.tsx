import type { Metadata } from "next";
import PedalBackground from "./PedalBackground";
import "./globals.css";

export const metadata: Metadata = {
  title: "PedalKeys — foot pedal controls for YouTube and any app",
  description:
    "Make a Sanguine Preon or VEC Infinity USB foot pedal seek back, play/pause and seek forward in YouTube, Chrome or any focused app. Pre-set, no setup screen.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en">
      <body>
        <PedalBackground />
        {children}
      </body>
    </html>
  );
}
