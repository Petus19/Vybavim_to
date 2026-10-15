import { LogoMark } from "@/components/logo";
import { ChevronLink } from "@/components/chevron-link";

export function ComingSoon({ title, text }: { title: string; text: string }) {
  return (
    <div className="mx-auto flex max-w-lg flex-col items-center px-5 pt-28 text-center">
      <LogoMark className="h-10" />
      <h1 className="mt-8 text-4xl font-semibold tracking-tight">{title}</h1>
      <p className="mt-3 text-lg text-muted-foreground">{text}</p>
      <ChevronLink href="/inzeraty" className="mt-8">
        Prezrieť inzeráty
      </ChevronLink>
    </div>
  );
}
