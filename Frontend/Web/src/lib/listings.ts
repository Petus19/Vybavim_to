/**
 * Dočasné mock dáta pre inzeráty.
 *
 * Tvar typov zodpovedá tomu, čo bude vracať backend (GET /api/v1/listings).
 * Keď bude API hotové, tento súbor nahradí typovaný klient vygenerovaný
 * z OpenAPI (Frontend/Shared) a stránky zostanú rovnaké.
 *
 * Dátumy sú pevné (nie „pred X dňami“), aby sme mali všetci rovnaké dáta.
 */

export type CategoryId = "web" | "grafika" | "it-podpora" | "marketing";

export type Category = {
  id: CategoryId;
  name: string;
  description: string;
};

export type Budget =
  { type: "fixed"; amount: number } | { type: "hourly"; amount: number } | { type: "negotiable" };

export type ListingStatus = "open" | "in_progress" | "completed";

/** Akú skúsenosť zadávateľ očakáva od odborníka */
export type ExperienceLevel = "beginner" | "intermediate" | "expert";

export type ProjectSize = "small" | "medium" | "large";

/** Jednorazová zákazka alebo dlhodobá spolupráca */
export type Engagement = "one_time" | "ongoing";

export type Listing = {
  id: string;
  title: string;
  description: string;
  category: CategoryId;
  city: string;
  remote: boolean;
  budget: Budget;
  status: ListingStatus;
  createdAt: string;
  deadline?: string;
  responses: number;
  level: ExperienceLevel;
  /** Veľkosť projektu a odhad trvania, napr. { size: "medium", duration: "2–4 týždne" } */
  scope: { size: ProjectSize; duration: string };
  engagement: Engagement;
  /** Zadávateľ potrebuje faktúru (odborník musí mať živnosť alebo firmu) */
  invoiceRequired: boolean;
  skills: string[];
  /** Čo má byť výsledkom – voliteľné, na detaile sa zobrazí iba ak je vyplnené */
  deliverables?: string[];
  /** Požiadavky na odborníka (prax, portfólio, jazyk…) – voliteľné */
  requirements?: string[];
  owner: {
    name: string;
    type: "person" | "company";
    rating: number;
    reviewCount: number;
    verified: boolean;
    projectCount: number;
    memberSince: string;
    responseTime: string;
  };
};

export const categories: Category[] = [
  { id: "web", name: "Web a aplikácie", description: "Weby, e-shopy, mobilné appky" },
  { id: "grafika", name: "Grafika a logo", description: "Logá, vizuály, tlačoviny" },
  { id: "it-podpora", name: "IT podpora", description: "Siete, počítače, servery" },
  { id: "marketing", name: "Online marketing", description: "Reklamy, sociálne siete, SEO" },
];

export const cities = [
  "Bratislava",
  "Košice",
  "Žilina",
  "Banská Bystrica",
  "Nitra",
  "Prešov",
  "Trnava",
  "Trenčín",
];

export const levelLabels: Record<ExperienceLevel, string> = {
  beginner: "Začiatočník",
  intermediate: "Skúsený",
  expert: "Expert",
};

export const levelDescriptions: Record<ExperienceLevel, string> = {
  beginner: "Vhodné aj pre študentov a začínajúcich",
  intermediate: "Niekoľko podobných projektov za sebou",
  expert: "Dlhoročná prax a silné portfólio",
};

export const sizeLabels: Record<ProjectSize, string> = {
  small: "Malý",
  medium: "Stredný",
  large: "Veľký",
};

export const engagementLabels: Record<Engagement, string> = {
  one_time: "Jednorazovo",
  ongoing: "Dlhodobo",
};

export const listings: Listing[] = [
  {
    id: "1",
    title: "Logo a vizuálna identita pre kaviareň",
    description:
      "Otvárame malú kaviareň v Starom Meste a hľadáme grafika na logo, farebnú paletu a jednoduchý manuál. Páči sa nám čistý, moderný štýl s trochou retra. Výstup potrebujeme vo vektoroch a pripravený na tlač aj web.",
    category: "grafika",
    city: "Bratislava",
    remote: true,
    budget: { type: "fixed", amount: 300 },
    status: "open",
    createdAt: "2026-10-12T09:30:00Z",
    deadline: "2026-11-15",
    responses: 7,
    level: "intermediate",
    scope: { size: "medium", duration: "2–4 týždne" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["Branding", "Adobe Illustrator", "Typografia", "Tlačové podklady"],
    deliverables: [
      "Logo vo vektoroch (SVG, PDF)",
      "Farebná paleta a písma",
      "Jednoduchý manuál (5–10 strán)",
    ],
    requirements: ["Portfólio s podobnými logami", "Komunikácia po slovensky"],
    owner: {
      name: "Kaviareň Pod Hradom",
      type: "company",
      rating: 4.8,
      reviewCount: 3,
      verified: true,
      projectCount: 5,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "2",
    title: "E-shop s domácou kozmetikou na Shoptete",
    description:
      "Predávam domácu prírodnú kozmetiku cez Instagram a chcem prejsť na vlastný e-shop. Hľadám niekoho, kto nastaví Shoptet, pripraví šablónu podľa našich farieb, nahrá prvých 30 produktov a prepojí platobnú bránu a dopravcov.",
    category: "web",
    city: "Košice",
    remote: true,
    budget: { type: "fixed", amount: 1200 },
    status: "open",
    createdAt: "2026-10-11T14:05:00Z",
    deadline: "2026-12-01",
    responses: 12,
    level: "intermediate",
    scope: { size: "medium", duration: "4–6 týždňov" },
    engagement: "one_time",
    invoiceRequired: false,
    skills: ["Shoptet", "E-commerce", "Platobné brány", "Webdizajn"],
    deliverables: [
      "Nastavený e-shop na Shoptete",
      "Šablóna vo farbách značky",
      "30 nahraných produktov",
      "Prepojená platobná brána a dopravcovia",
    ],
    requirements: ["Aspoň 2 e-shopy na Shoptete v portfóliu"],
    owner: {
      name: "Jana Kováčová",
      type: "person",
      rating: 5,
      reviewCount: 2,
      verified: false,
      projectCount: 1,
      memberSince: "2026",
      responseTime: "do 2 dní",
    },
  },
  {
    id: "3",
    title: "Správa siete a počítačov v malej kancelárii",
    description:
      "Máme kanceláriu s 8 počítačmi, tlačiarňou a NAS. Hľadáme spoľahlivého IT technika na pravidelnú správu (cca 4 hodiny mesačne), zálohovanie a riešenie problémov podľa potreby. Ideálne niekto zo Žiliny alebo okolia.",
    category: "it-podpora",
    city: "Žilina",
    remote: false,
    budget: { type: "hourly", amount: 25 },
    status: "open",
    createdAt: "2026-10-10T08:00:00Z",
    responses: 4,
    level: "intermediate",
    scope: { size: "small", duration: "cca 4 hodiny mesačne" },
    engagement: "ongoing",
    invoiceRequired: true,
    skills: ["Windows", "Počítačové siete", "NAS", "Zálohovanie"],
    deliverables: [
      "Pravidelná kontrola a aktualizácie",
      "Nastavené automatické zálohovanie",
      "Riešenie problémov do 48 hodín",
    ],
    requirements: ["Práca na mieste v Žiline alebo okolí", "Skúsenosti so Synology NAS"],
    owner: {
      name: "Účtovníctvo Mráz s.r.o.",
      type: "company",
      rating: 4.5,
      reviewCount: 6,
      verified: true,
      projectCount: 8,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "4",
    title: "Kampaň na Facebooku a Instagrame pre autoservis",
    description:
      "Chceme osloviť viac zákazníkov z okolia na sezónne prezutie a servis klimatizácie. Hľadáme človeka, ktorý pripraví a spustí kampaň, nastaví cielenie a po mesiaci vyhodnotí výsledky.",
    category: "marketing",
    city: "Nitra",
    remote: true,
    budget: { type: "fixed", amount: 450 },
    status: "open",
    createdAt: "2026-10-09T16:20:00Z",
    deadline: "2026-10-31",
    responses: 5,
    level: "intermediate",
    scope: { size: "small", duration: "1 mesiac" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["Meta Ads", "Cielenie reklám", "Reporting"],
    deliverables: [
      "Nastavená kampaň na Facebooku a Instagrame",
      "3 varianty reklám",
      "Vyhodnotenie výsledkov po mesiaci",
    ],
    requirements: ["Ukážky výsledkov z predchádzajúcich kampaní"],
    owner: {
      name: "AutoServis Kubica",
      type: "company",
      rating: 4.2,
      reviewCount: 9,
      verified: true,
      projectCount: 11,
      memberSince: "2025",
      responseTime: "do 3 hodín",
    },
  },
  {
    id: "5",
    title: "Mobilná appka na rezervácie termínov v kaderníctve",
    description:
      "Máme tri prevádzky a chceme, aby si zákazníci vedeli rezervovať termín cez mobil. Potrebujeme appku pre iOS aj Android, jednoduchú administráciu pre kaderníčky a pripomienky cez SMS alebo notifikácie.",
    category: "web",
    city: "Banská Bystrica",
    remote: true,
    budget: { type: "negotiable" },
    status: "open",
    createdAt: "2026-10-08T10:45:00Z",
    responses: 9,
    level: "expert",
    scope: { size: "large", duration: "3–6 mesiacov" },
    engagement: "ongoing",
    invoiceRequired: true,
    skills: ["React Native", "iOS", "Android", "Push notifikácie", "Backend"],
    deliverables: [
      "Appka pre iOS a Android",
      "Administrácia pre kaderníčky",
      "Pripomienky cez SMS alebo notifikácie",
    ],
    requirements: [
      "Aspoň 3 roky praxe s mobilnými aplikáciami",
      "Appky zverejnené v App Store a Google Play",
    ],
    owner: {
      name: "Salón Bella",
      type: "company",
      rating: 4.9,
      reviewCount: 4,
      verified: true,
      projectCount: 4,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "6",
    title: "Grafika na obaly lokálneho medu",
    description:
      "Sme rodinná včelárska farma a chceme nové etikety na poháre medu (5 druhov). Štýl by mal pôsobiť prírodne a prémiovo. Súčasťou je aj príprava podkladov pre tlačiareň.",
    category: "grafika",
    city: "Prešov",
    remote: true,
    budget: { type: "fixed", amount: 250 },
    status: "open",
    createdAt: "2026-10-07T12:00:00Z",
    deadline: "2026-11-30",
    responses: 6,
    level: "beginner",
    scope: { size: "small", duration: "1–2 týždne" },
    engagement: "one_time",
    invoiceRequired: false,
    skills: ["Grafický dizajn", "Etikety a obaly", "Tlačové podklady"],
    deliverables: ["5 etikiet (jedna pre každý druh medu)", "Podklady pre tlačiareň"],
    owner: {
      name: "Martin Hudák",
      type: "person",
      rating: 4.7,
      reviewCount: 1,
      verified: false,
      projectCount: 1,
      memberSince: "2026",
      responseTime: "do 2 dní",
    },
  },
  {
    id: "7",
    title: "Oprava a zrýchlenie WordPress webu",
    description:
      "Náš firemný web na WordPresse sa načítava veľmi pomaly a niektoré stránky na mobile nefungujú správne. Hľadáme niekoho, kto web zrýchli, aktualizuje pluginy a opraví chyby v zobrazení.",
    category: "web",
    city: "Trnava",
    remote: true,
    budget: { type: "hourly", amount: 30 },
    status: "open",
    createdAt: "2026-10-06T09:15:00Z",
    responses: 8,
    level: "intermediate",
    scope: { size: "small", duration: "do 2 týždňov" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["WordPress", "PHP", "Výkon webu", "Responzívny dizajn"],
    deliverables: [
      "Zrýchlené načítavanie (PageSpeed nad 80)",
      "Aktualizované pluginy",
      "Opravené zobrazenie na mobile",
    ],
    owner: {
      name: "Stavby Trnava a.s.",
      type: "company",
      rating: 4.4,
      reviewCount: 12,
      verified: true,
      projectCount: 14,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "8",
    title: "Nastavenie Microsoft 365 a e-mailov pre 15 ľudí",
    description:
      "Prechádzame zo starého e-mailového hostingu na Microsoft 365. Potrebujeme migráciu schránok, nastavenie Teams, zdieľaných priečinkov a krátke zaškolenie zamestnancov.",
    category: "it-podpora",
    city: "Trenčín",
    remote: false,
    budget: { type: "fixed", amount: 800 },
    status: "open",
    createdAt: "2026-10-05T13:30:00Z",
    deadline: "2026-11-10",
    responses: 3,
    level: "intermediate",
    scope: { size: "medium", duration: "2–3 týždne" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["Microsoft 365", "Exchange", "Teams", "Migrácia e-mailov"],
    deliverables: [
      "Migrované schránky 15 ľudí",
      "Nastavené Teams a zdieľané priečinky",
      "Zaškolenie zamestnancov",
    ],
    requirements: ["Certifikácia Microsoft je výhodou"],
    owner: {
      name: "Pekáreň Zlatý klas",
      type: "company",
      rating: 4.6,
      reviewCount: 2,
      verified: true,
      projectCount: 2,
      memberSince: "2026",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "9",
    title: "SEO audit a texty pre web penziónu",
    description:
      "Penzión v Tatrách potrebuje lepšie pozície vo vyhľadávaní. Hľadáme SEO špecialistu na audit webu, návrh kľúčových slov a prepis textov na hlavných stránkach.",
    category: "marketing",
    city: "Prešov",
    remote: true,
    budget: { type: "fixed", amount: 600 },
    status: "open",
    createdAt: "2026-10-04T07:50:00Z",
    responses: 2,
    level: "intermediate",
    scope: { size: "medium", duration: "1–2 mesiace" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["SEO", "Copywriting", "Google Search Console"],
    deliverables: ["SEO audit webu", "Návrh kľúčových slov", "Prepísané texty hlavných stránok"],
    requirements: ["Výsledky SEO pre podobný web (ubytovanie, cestovný ruch)"],
    owner: {
      name: "Penzión Lesná",
      type: "company",
      rating: 5,
      reviewCount: 5,
      verified: true,
      projectCount: 5,
      memberSince: "2025",
      responseTime: "do 2 dní",
    },
  },
  {
    id: "10",
    title: "Ilustrácie do detskej knihy",
    description:
      "Píšem detskú knihu o zvieratkách zo slovenských lesov a hľadám ilustrátora na 12 celostranových ilustrácií a obálku. Štýl akvarel alebo jemná digitálna maľba.",
    category: "grafika",
    city: "Bratislava",
    remote: true,
    budget: { type: "negotiable" },
    status: "open",
    createdAt: "2026-10-03T18:10:00Z",
    responses: 11,
    level: "expert",
    scope: { size: "large", duration: "2–3 mesiace" },
    engagement: "one_time",
    invoiceRequired: false,
    skills: ["Ilustrácia", "Akvarel", "Digitálna maľba"],
    deliverables: ["12 celostranových ilustrácií", "Obálka knihy"],
    requirements: ["Portfólio detských ilustrácií"],
    owner: {
      name: "Zuzana Lehotská",
      type: "person",
      rating: 4.9,
      reviewCount: 2,
      verified: false,
      projectCount: 2,
      memberSince: "2026",
      responseTime: "do 3 dní",
    },
  },
  {
    id: "11",
    title: "Landing page pre novú fitness aplikáciu",
    description:
      "Spúšťame fitness aplikáciu a potrebujeme jednostránkový web s registráciou do čakacieho zoznamu, napojením na Mailchimp a jednoduchou analytikou.",
    category: "web",
    city: "Bratislava",
    remote: true,
    budget: { type: "fixed", amount: 700 },
    status: "in_progress",
    createdAt: "2026-09-28T11:00:00Z",
    responses: 15,
    level: "beginner",
    scope: { size: "small", duration: "1–2 týždne" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["HTML a CSS", "Mailchimp", "Webová analytika"],
    deliverables: [
      "Jednostránkový web",
      "Registrácia do čakacieho zoznamu",
      "Napojenie na Mailchimp",
    ],
    owner: {
      name: "FitNow s.r.o.",
      type: "company",
      rating: 4.3,
      reviewCount: 7,
      verified: true,
      projectCount: 7,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
  {
    id: "12",
    title: "Inštalácia kamerového systému a Wi-Fi v predajni",
    description:
      "V novej predajni potrebujeme 4 kamery s nahrávaním, pokrytie Wi-Fi pre zákazníkov aj pokladňu a oddelenú sieť pre pokladničný systém.",
    category: "it-podpora",
    city: "Košice",
    remote: false,
    budget: { type: "fixed", amount: 950 },
    status: "completed",
    createdAt: "2026-09-20T15:40:00Z",
    responses: 6,
    level: "intermediate",
    scope: { size: "small", duration: "1 týždeň" },
    engagement: "one_time",
    invoiceRequired: true,
    skills: ["Kamerové systémy", "Wi-Fi", "Počítačové siete"],
    deliverables: [
      "4 kamery s nahrávaním",
      "Wi-Fi pre zákazníkov aj pokladňu",
      "Oddelená sieť pre pokladničný systém",
    ],
    requirements: ["Práca na mieste v Košiciach"],
    owner: {
      name: "Kvetinárstvo Ruža",
      type: "company",
      rating: 4.8,
      reviewCount: 3,
      verified: true,
      projectCount: 3,
      memberSince: "2025",
      responseTime: "do 1 dňa",
    },
  },
];

export type BudgetType = Budget["type"];

export type SortOrder = "newest" | "cheapest" | "priciest";

export type PriceRange = { min?: number; max?: number };

export type ListingFilters = {
  q?: string;
  category?: string;
  city?: string;
  budgetType?: BudgetType;
  /** Rozsah pre pevnú cenu (€) */
  fixedPrice?: PriceRange;
  /** Rozsah pre hodinovú sadzbu (€/hod.) – s pevnou cenou sa nemieša */
  hourlyRate?: PriceRange;
  level?: ExperienceLevel;
  engagement?: Engagement;
  remoteOnly?: boolean;
  openOnly?: boolean;
  /** Iba inzeráty, ktoré nevyžadujú faktúru (napr. pre študentov bez živnosti) */
  noInvoiceOnly?: boolean;
  sort?: SortOrder;
};

function hasRange(range?: PriceRange) {
  return range?.min !== undefined || range?.max !== undefined;
}

function inRange(value: number, range: PriceRange) {
  return (
    (range.min === undefined || value >= range.min) &&
    (range.max === undefined || value <= range.max)
  );
}

/**
 * Cenové rozsahy: pevná cena sa porovnáva iba s rozsahom pevnej ceny a hodinová sadzba
 * iba s rozsahom sadzby. Ak je zadaný aspoň jeden rozsah, zobrazia sa len inzeráty,
 * ktorých typ ceny má zadaný rozsah (a inzeráty „dohodou“ sa skryjú, lebo cenu nemajú).
 */
function matchesPrice(budget: Budget, filters: ListingFilters) {
  const fixed = hasRange(filters.fixedPrice);
  const hourly = hasRange(filters.hourlyRate);
  if (!fixed && !hourly) return true;
  if (budget.type === "fixed") return fixed && inRange(budget.amount, filters.fixedPrice!);
  if (budget.type === "hourly") return hourly && inRange(budget.amount, filters.hourlyRate!);
  return false;
}

const budgetTypeOrder: Record<BudgetType, number> = { fixed: 0, hourly: 1, negotiable: 2 };

/**
 * Zoradenie podľa ceny: najprv pevné ceny, potom hodinové sadzby, nakoniec „dohodou“.
 * V rámci skupiny podľa sumy – rôzne typy cien sa navzájom neporovnávajú.
 */
function compareByPrice(a: Listing, b: Listing, direction: 1 | -1) {
  const byType = budgetTypeOrder[a.budget.type] - budgetTypeOrder[b.budget.type];
  if (byType !== 0) return byType;
  const amountA = a.budget.type === "negotiable" ? 0 : a.budget.amount;
  const amountB = b.budget.type === "negotiable" ? 0 : b.budget.amount;
  return (amountA - amountB) * direction;
}

/** Odstráni diakritiku a veľké písmená, aby „kaviaren“ našlo „Kaviareň“. */
function normalize(text: string) {
  return text
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
}

export function getListings(filters: ListingFilters = {}): Listing[] {
  const query = filters.q ? normalize(filters.q.trim()) : "";

  return listings
    .filter(
      (l) =>
        !query || normalize(`${l.title} ${l.description} ${l.skills.join(" ")}`).includes(query),
    )
    .filter((l) => !filters.category || l.category === filters.category)
    .filter((l) => !filters.city || l.city === filters.city)
    .filter((l) => !filters.budgetType || l.budget.type === filters.budgetType)
    .filter((l) => matchesPrice(l.budget, filters))
    .filter((l) => !filters.level || l.level === filters.level)
    .filter((l) => !filters.engagement || l.engagement === filters.engagement)
    .filter((l) => !filters.remoteOnly || l.remote)
    .filter((l) => !filters.openOnly || l.status === "open")
    .filter((l) => !filters.noInvoiceOnly || !l.invoiceRequired)
    .toSorted((a, b) => {
      if (filters.sort === "cheapest") return compareByPrice(a, b, 1);
      if (filters.sort === "priciest") return compareByPrice(a, b, -1);
      return b.createdAt.localeCompare(a.createdAt);
    });
}

export function getListing(id: string): Listing | undefined {
  return listings.find((l) => l.id === id);
}

/** Otvorené inzeráty z rovnakej kategórie (bez aktuálneho), najnovšie prvé. */
export function getSimilarListings(listing: Listing, limit = 3): Listing[] {
  return getListings({ category: listing.category, openOnly: true })
    .filter((l) => l.id !== listing.id)
    .slice(0, limit);
}

export function getCategory(id: CategoryId): Category {
  return categories.find((c) => c.id === id)!;
}

const euro = new Intl.NumberFormat("sk-SK", {
  style: "currency",
  currency: "EUR",
  maximumFractionDigits: 0,
});

export function formatBudget(budget: Budget): string {
  switch (budget.type) {
    case "fixed":
      return euro.format(budget.amount);
    case "hourly":
      return `${euro.format(budget.amount)} / hod.`;
    case "negotiable":
      return "Dohodou";
  }
}

const dateFormat = new Intl.DateTimeFormat("sk-SK", {
  day: "numeric",
  month: "numeric",
  year: "numeric",
  timeZone: "Europe/Bratislava",
});

export function formatDate(iso: string): string {
  return dateFormat.format(new Date(iso));
}

const pluralRules = new Intl.PluralRules("sk-SK");

/** Slovenské tvary podľa počtu: plural(4, ["inzerát", "inzeráty", "inzerátov"]) → "4 inzeráty" */
export function plural(count: number, [one, few, many]: [string, string, string]): string {
  const rule = pluralRules.select(count);
  const word = rule === "one" ? one : rule === "few" ? few : many;
  return `${count} ${word}`;
}

export const statusLabels: Record<ListingStatus, string> = {
  open: "Otvorený",
  in_progress: "Prebieha",
  completed: "Dokončený",
};
