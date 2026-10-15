import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { SiteHeader } from "@/components/site-header";
import { SiteFooter } from "@/components/site-footer";
import { themeInitScript } from "@/lib/theme";
import "./globals.css";

// Inter je najbližší voľne dostupný ekvivalent systémového písma Apple (SF Pro).
// latin-ext je nutný kvôli slovenským znakom (ľ, ť, ň, ô, ä …).
const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin", "latin-ext"],
});

export const metadata: Metadata = {
  title: {
    default: "Vybavím to – nájdi odborníka na čokoľvek",
    template: "%s · Vybavím to",
  },
  description:
    "Zadaj, čo potrebuješ vybaviť, a odborníci z IT a grafiky na Slovensku ti pošlú ponuky.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // suppressHydrationWarning: data-theme na <html> nastaví skript ešte pred Reactom
    <html lang="sk" className={`${inter.variable} h-full antialiased`} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className="flex min-h-full flex-col">
        <SiteHeader />
        <main className="flex-1">{children}</main>
        <SiteFooter />
      </body>
    </html>
  );
}
