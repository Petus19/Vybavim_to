using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Infrastructure.Seeding;

/// <summary>
/// Testovacie inzeráty pre vývoj – rovnaké ako mock dáta webu (Frontend/Web/src/lib/listings.ts),
/// aby web po napojení na API ukazoval to isté ako teraz.
/// Spúšťa sa iba vo vývoji a iba ak je tabuľka inzerátov prázdna.
/// </summary>
internal sealed class ListingsDevSeeder(ListingsDbContext db) : IDevDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Už sú tam inzeráty? Tak nič nepridávame, inak by sa pri každom štarte pridalo ďalších 12.
        if (await db.Listings.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Listings.AddRange(CreateListings());
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Vymyslení zadávatelia. Tabuľka používateľov zatiaľ neexistuje; keď pribudne modul Identity,
    /// jeho seeder vytvorí používateľov presne s týmito ID a inzeráty sa k nim „pripoja“.
    /// </summary>
    private static class Owners
    {
        public static readonly Guid KaviarenPodHradom = Guid.Parse("019a0b2c-2000-7000-8000-000000000001");
        public static readonly Guid JanaKovacova = Guid.Parse("019a0b2c-2000-7000-8000-000000000002");
        public static readonly Guid UctovnictvoMraz = Guid.Parse("019a0b2c-2000-7000-8000-000000000003");
        public static readonly Guid AutoServisKubica = Guid.Parse("019a0b2c-2000-7000-8000-000000000004");
        public static readonly Guid SalonBella = Guid.Parse("019a0b2c-2000-7000-8000-000000000005");
        public static readonly Guid MartinHudak = Guid.Parse("019a0b2c-2000-7000-8000-000000000006");
        public static readonly Guid StavbyTrnava = Guid.Parse("019a0b2c-2000-7000-8000-000000000007");
        public static readonly Guid PekarenZlatyKlas = Guid.Parse("019a0b2c-2000-7000-8000-000000000008");
        public static readonly Guid PenzionLesna = Guid.Parse("019a0b2c-2000-7000-8000-000000000009");
        public static readonly Guid ZuzanaLehotska = Guid.Parse("019a0b2c-2000-7000-8000-000000000010");
        public static readonly Guid FitNow = Guid.Parse("019a0b2c-2000-7000-8000-000000000011");
        public static readonly Guid KvetinarstvoRuza = Guid.Parse("019a0b2c-2000-7000-8000-000000000012");
    }

    /// <summary>Pevný čas v UTC, aby mali všetci vývojári rovnaké dáta.</summary>
    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static List<Listing> CreateListings()
    {
        // Všetky inzeráty idú cez Listing.Create() – rovnaké pravidlá ako pri skutočných.
        var listings = new List<Listing>
        {
            Listing.Create(
                ownerId: Owners.KaviarenPodHradom,
                categoryId: CategoryIds.Graphics,
                title: "Logo a vizuálna identita pre kaviareň",
                description: "Otvárame malú kaviareň v Starom Meste a hľadáme grafika na logo, farebnú paletu a jednoduchý manuál. Páči sa nám čistý, moderný štýl s trochou retra. Výstup potrebujeme vo vektoroch a pripravený na tlač aj web.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 300,
                city: "Bratislava",
                remote: true,
                deadline: new DateOnly(2026, 11, 15),
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Medium,
                estimatedDuration: "2–4 týždne",
                engagement: Engagement.OneTime,
                invoiceRequired: true,
                skills: ["Branding", "Adobe Illustrator", "Typografia", "Tlačové podklady"],
                deliverables: ["Logo vo vektoroch (SVG, PDF)", "Farebná paleta a písma", "Jednoduchý manuál (5–10 strán)"],
                requirements: ["Portfólio s podobnými logami", "Komunikácia po slovensky"],
                now: At(2026, 10, 12, 9, 30)),

            Listing.Create(
                ownerId: Owners.JanaKovacova,
                categoryId: CategoryIds.Web,
                title: "E-shop s domácou kozmetikou na Shoptete",
                description: "Predávam domácu prírodnú kozmetiku cez Instagram a chcem prejsť na vlastný e-shop. Hľadám niekoho, kto nastaví Shoptet, pripraví šablónu podľa našich farieb, nahrá prvých 30 produktov a prepojí platobnú bránu a dopravcov.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 1200,
                city: "Košice",
                remote: true,
                deadline: new DateOnly(2026, 12, 1),
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Medium,
                estimatedDuration: "4–6 týždňov",
                engagement: Engagement.OneTime,
                invoiceRequired: false,
                skills: ["Shoptet", "E-commerce", "Platobné brány", "Webdizajn"],
                deliverables: ["Nastavený e-shop na Shoptete", "Šablóna vo farbách značky", "30 nahraných produktov", "Prepojená platobná brána a dopravcovia"],
                requirements: ["Aspoň 2 e-shopy na Shoptete v portfóliu"],
                now: At(2026, 10, 11, 14, 5)),

            Listing.Create(
                ownerId: Owners.UctovnictvoMraz,
                categoryId: CategoryIds.ItSupport,
                title: "Správa siete a počítačov v malej kancelárii",
                description: "Máme kanceláriu s 8 počítačmi, tlačiarňou a NAS. Hľadáme spoľahlivého IT technika na pravidelnú správu (cca 4 hodiny mesačne), zálohovanie a riešenie problémov podľa potreby. Ideálne niekto zo Žiliny alebo okolia.",
                budgetType: BudgetType.Hourly,
                budgetAmount: 25,
                city: "Žilina",
                remote: false,
                deadline: null,
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Small,
                estimatedDuration: "cca 4 hodiny mesačne",
                engagement: Engagement.Ongoing,
                invoiceRequired: true,
                skills: ["Windows", "Počítačové siete", "NAS", "Zálohovanie"],
                deliverables: ["Pravidelná kontrola a aktualizácie", "Nastavené automatické zálohovanie", "Riešenie problémov do 48 hodín"],
                requirements: ["Práca na mieste v Žiline alebo okolí", "Skúsenosti so Synology NAS"],
                now: At(2026, 10, 10, 8, 0)),

            Listing.Create(
                ownerId: Owners.AutoServisKubica,
                categoryId: CategoryIds.Marketing,
                title: "Kampaň na Facebooku a Instagrame pre autoservis",
                description: "Chceme osloviť viac zákazníkov z okolia na sezónne prezutie a servis klimatizácie. Hľadáme človeka, ktorý pripraví a spustí kampaň, nastaví cielenie a po mesiaci vyhodnotí výsledky.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 450,
                city: "Nitra",
                remote: true,
                deadline: new DateOnly(2026, 10, 31),
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Small,
                estimatedDuration: "1 mesiac",
                engagement: Engagement.OneTime,
                invoiceRequired: true,
                skills: ["Meta Ads", "Cielenie reklám", "Reporting"],
                deliverables: ["Nastavená kampaň na Facebooku a Instagrame", "3 varianty reklám", "Vyhodnotenie výsledkov po mesiaci"],
                requirements: ["Ukážky výsledkov z predchádzajúcich kampaní"],
                now: At(2026, 10, 9, 16, 20)),

            Listing.Create(
                ownerId: Owners.SalonBella,
                categoryId: CategoryIds.Web,
                title: "Mobilná appka na rezervácie termínov v kaderníctve",
                description: "Máme tri prevádzky a chceme, aby si zákazníci vedeli rezervovať termín cez mobil. Potrebujeme appku pre iOS aj Android, jednoduchú administráciu pre kaderníčky a pripomienky cez SMS alebo notifikácie.",
                budgetType: BudgetType.Negotiable,
                budgetAmount: null,
                city: "Banská Bystrica",
                remote: true,
                deadline: null,
                level: ExperienceLevel.Expert,
                size: ProjectSize.Large,
                estimatedDuration: "3–6 mesiacov",
                engagement: Engagement.Ongoing,
                invoiceRequired: true,
                skills: ["React Native", "iOS", "Android", "Push notifikácie", "Backend"],
                deliverables: ["Appka pre iOS a Android", "Administrácia pre kaderníčky", "Pripomienky cez SMS alebo notifikácie"],
                requirements: ["Aspoň 3 roky praxe s mobilnými aplikáciami", "Appky zverejnené v App Store a Google Play"],
                now: At(2026, 10, 8, 10, 45)),

            Listing.Create(
                ownerId: Owners.MartinHudak,
                categoryId: CategoryIds.Graphics,
                title: "Grafika na obaly lokálneho medu",
                description: "Sme rodinná včelárska farma a chceme nové etikety na poháre medu (5 druhov). Štýl by mal pôsobiť prírodne a prémiovo. Súčasťou je aj príprava podkladov pre tlačiareň.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 250,
                city: "Prešov",
                remote: true,
                deadline: new DateOnly(2026, 11, 30),
                level: ExperienceLevel.Beginner,
                size: ProjectSize.Small,
                estimatedDuration: "1–2 týždne",
                engagement: Engagement.OneTime,
                invoiceRequired: false,
                skills: ["Grafický dizajn", "Etikety a obaly", "Tlačové podklady"],
                deliverables: ["5 etikiet (jedna pre každý druh medu)", "Podklady pre tlačiareň"],
                requirements: null,
                now: At(2026, 10, 7, 12, 0)),

            Listing.Create(
                ownerId: Owners.StavbyTrnava,
                categoryId: CategoryIds.Web,
                title: "Oprava a zrýchlenie WordPress webu",
                description: "Náš firemný web na WordPresse sa načítava veľmi pomaly a niektoré stránky na mobile nefungujú správne. Hľadáme niekoho, kto web zrýchli, aktualizuje pluginy a opraví chyby v zobrazení.",
                budgetType: BudgetType.Hourly,
                budgetAmount: 30,
                city: "Trnava",
                remote: true,
                deadline: null,
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Small,
                estimatedDuration: "do 2 týždňov",
                engagement: Engagement.OneTime,
                invoiceRequired: true,
                skills: ["WordPress", "PHP", "Výkon webu", "Responzívny dizajn"],
                deliverables: ["Zrýchlené načítavanie (PageSpeed nad 80)", "Aktualizované pluginy", "Opravené zobrazenie na mobile"],
                requirements: null,
                now: At(2026, 10, 6, 9, 15)),

            Listing.Create(
                ownerId: Owners.PekarenZlatyKlas,
                categoryId: CategoryIds.ItSupport,
                title: "Nastavenie Microsoft 365 a e-mailov pre 15 ľudí",
                description: "Prechádzame zo starého e-mailového hostingu na Microsoft 365. Potrebujeme migráciu schránok, nastavenie Teams, zdieľaných priečinkov a krátke zaškolenie zamestnancov.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 800,
                city: "Trenčín",
                remote: false,
                deadline: new DateOnly(2026, 11, 10),
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Medium,
                estimatedDuration: "2–3 týždne",
                engagement: Engagement.OneTime,
                invoiceRequired: true,
                skills: ["Microsoft 365", "Exchange", "Teams", "Migrácia e-mailov"],
                deliverables: ["Migrované schránky 15 ľudí", "Nastavené Teams a zdieľané priečinky", "Zaškolenie zamestnancov"],
                requirements: ["Certifikácia Microsoft je výhodou"],
                now: At(2026, 10, 5, 13, 30)),

            Listing.Create(
                ownerId: Owners.PenzionLesna,
                categoryId: CategoryIds.Marketing,
                title: "SEO audit a texty pre web penziónu",
                description: "Penzión v Tatrách potrebuje lepšie pozície vo vyhľadávaní. Hľadáme SEO špecialistu na audit webu, návrh kľúčových slov a prepis textov na hlavných stránkach.",
                budgetType: BudgetType.Fixed,
                budgetAmount: 600,
                city: "Prešov",
                remote: true,
                deadline: null,
                level: ExperienceLevel.Intermediate,
                size: ProjectSize.Medium,
                estimatedDuration: "1–2 mesiace",
                engagement: Engagement.OneTime,
                invoiceRequired: true,
                skills: ["SEO", "Copywriting", "Google Search Console"],
                deliverables: ["SEO audit webu", "Návrh kľúčových slov", "Prepísané texty hlavných stránok"],
                requirements: ["Výsledky SEO pre podobný web (ubytovanie, cestovný ruch)"],
                now: At(2026, 10, 4, 7, 50)),

            Listing.Create(
                ownerId: Owners.ZuzanaLehotska,
                categoryId: CategoryIds.Graphics,
                title: "Ilustrácie do detskej knihy",
                description: "Píšem detskú knihu o zvieratkách zo slovenských lesov a hľadám ilustrátora na 12 celostranových ilustrácií a obálku. Štýl akvarel alebo jemná digitálna maľba.",
                budgetType: BudgetType.Negotiable,
                budgetAmount: null,
                city: "Bratislava",
                remote: true,
                deadline: null,
                level: ExperienceLevel.Expert,
                size: ProjectSize.Large,
                estimatedDuration: "2–3 mesiace",
                engagement: Engagement.OneTime,
                invoiceRequired: false,
                skills: ["Ilustrácia", "Akvarel", "Digitálna maľba"],
                deliverables: ["12 celostranových ilustrácií", "Obálka knihy"],
                requirements: ["Portfólio detských ilustrácií"],
                now: At(2026, 10, 3, 18, 10)),
        };

        // Inzerát, na ktorom sa už pracuje
        var landingPage = Listing.Create(
            ownerId: Owners.FitNow,
            categoryId: CategoryIds.Web,
            title: "Landing page pre novú fitness aplikáciu",
            description: "Spúšťame fitness aplikáciu a potrebujeme jednostránkový web s registráciou do čakacieho zoznamu, napojením na Mailchimp a jednoduchou analytikou.",
            budgetType: BudgetType.Fixed,
            budgetAmount: 700,
            city: "Bratislava",
            remote: true,
            deadline: null,
            level: ExperienceLevel.Beginner,
            size: ProjectSize.Small,
            estimatedDuration: "1–2 týždne",
            engagement: Engagement.OneTime,
            invoiceRequired: true,
            skills: ["HTML a CSS", "Mailchimp", "Webová analytika"],
            deliverables: ["Jednostránkový web", "Registrácia do čakacieho zoznamu", "Napojenie na Mailchimp"],
            requirements: null,
            now: At(2026, 9, 28, 11, 0));
        landingPage.Start(At(2026, 10, 2, 10, 0));
        listings.Add(landingPage);

        // Dokončený inzerát
        var cameras = Listing.Create(
            ownerId: Owners.KvetinarstvoRuza,
            categoryId: CategoryIds.ItSupport,
            title: "Inštalácia kamerového systému a Wi-Fi v predajni",
            description: "V novej predajni potrebujeme 4 kamery s nahrávaním, pokrytie Wi-Fi pre zákazníkov aj pokladňu a oddelenú sieť pre pokladničný systém.",
            budgetType: BudgetType.Fixed,
            budgetAmount: 950,
            city: "Košice",
            remote: false,
            deadline: null,
            level: ExperienceLevel.Intermediate,
            size: ProjectSize.Small,
            estimatedDuration: "1 týždeň",
            engagement: Engagement.OneTime,
            invoiceRequired: true,
            skills: ["Kamerové systémy", "Wi-Fi", "Počítačové siete"],
            deliverables: ["4 kamery s nahrávaním", "Wi-Fi pre zákazníkov aj pokladňu", "Oddelená sieť pre pokladničný systém"],
            requirements: ["Práca na mieste v Košiciach"],
            now: At(2026, 9, 20, 15, 40));
        cameras.Start(At(2026, 9, 24, 9, 0));
        cameras.Complete(At(2026, 10, 1, 17, 0));
        listings.Add(cameras);

        return listings;
    }
}
