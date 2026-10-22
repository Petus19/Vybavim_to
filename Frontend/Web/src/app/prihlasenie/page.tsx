import type { Metadata } from "next";
import { Suspense } from "react";
import { redirect } from "next/navigation";
import { AuthForm } from "@/components/auth-form";
import { LogoMark } from "@/components/logo";
import { getCurrentUser, safeReturnPath } from "@/lib/auth";
import { isGoogleEnabled } from "@/lib/google";

export const metadata: Metadata = { title: "Prihlásenie" };

export default function LoginPage(props: PageProps<"/prihlasenie">) {
  return (
    <div className="mx-auto flex max-w-sm flex-col items-center px-5 pt-16 sm:pt-24">
      <LogoMark className="h-10" />
      {/* Čítame ?navrat= z URL a cookie (či už je prihlásený), preto Suspense */}
      <Suspense
        fallback={<div className="mt-8 h-96 w-full animate-pulse rounded-3xl bg-surface" />}
      >
        <LoginContent searchParams={props.searchParams} />
      </Suspense>
    </div>
  );
}

async function LoginContent({
  searchParams,
}: {
  searchParams: PageProps<"/prihlasenie">["searchParams"];
}) {
  const params = await searchParams;
  const navrat = safeReturnPath(params.navrat);

  // Prihlásený už nemá čo robiť na prihlásení – pošleme ho tam, kam smeroval
  if (await getCurrentUser()) redirect(navrat);

  const continuing = navrat !== "/";
  return (
    <>
      <h1 className="mt-6 text-center text-3xl font-semibold tracking-tight">
        {continuing ? "Najprv sa prihlás" : "Vitaj"}
      </h1>
      <p className="mt-2 mb-8 text-center text-muted-foreground">
        {continuing
          ? "Po prihlásení budeš pokračovať tam, kde si skončil."
          : "Prihlás sa alebo si vytvor účet."}
      </p>
      {params.chyba === "google" && (
        <p
          role="alert"
          className="mb-6 w-full rounded-2xl bg-destructive/10 px-4 py-3 text-center text-sm text-destructive"
        >
          Prihlásenie cez Google sa nepodarilo. Skús to znova alebo použi e-mail.
        </p>
      )}
      <AuthForm
        navrat={navrat}
        mode={params.rezim === "registracia" ? "register" : "login"}
        googleEnabled={isGoogleEnabled()}
      />
    </>
  );
}
