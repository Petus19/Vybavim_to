using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Projects.Domain;

namespace Vybavim.Modules.Projects.Infrastructure.Seeding;

/// <summary>
/// Pripravené ponuky na inzeráty demo zadávateľa client.demo, aby mal agent čo porovnávať:
/// dve konkurenčné ponuky na redizajn loga (skúsená grafička drahšie a rýchlejšie, začiatočník lacnejšie)
/// a dve na Google Ads. ID inzerátov a špecialistov sa musia zhodovať s ListingsDemoSeeder
/// a IdentityDemoSeeder (modul Projects nečíta tabuľky iných modulov) – overuje to test DemoDataTests.
/// Registruje sa iba vo vývoji so zapnutým <see cref="DevData.DemoAccountsKey"/>.
/// </summary>
internal sealed class ProjectsDemoSeeder(ProjectsDbContext db) : IDevDataSeeder
{
    private static readonly Guid LogoRedesignListing = Guid.Parse("019a0b2c-4000-7000-8000-000000000001");
    private static readonly Guid GoogleAdsListing = Guid.Parse("019a0b2c-4000-7000-8000-000000000002");

    private static readonly Guid LuciaBartosova = Guid.Parse("019a0b2c-3000-7000-8000-000000000002");
    private static readonly Guid MarekHruska = Guid.Parse("019a0b2c-3000-7000-8000-000000000003");
    private static readonly Guid KlikMarketing = Guid.Parse("019a0b2c-3000-7000-8000-000000000004");

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Offers.AnyAsync(o => o.ListingId == LogoRedesignListing, cancellationToken))
        {
            return;
        }

        var at = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
        db.Offers.AddRange(
            Offer.Create(
                LogoRedesignListing, LuciaBartosova, price: 280m, deliveryDays: 7,
                message: "Dobrý deň, logá pre autoservisy a dielne som robila už niekoľkokrát. Pripravím 3 návrhy " +
                         "modernizácie, ktoré zachovajú to, podľa čoho vás zákazníci poznajú, a 2 kolá úprav. " +
                         "Výstup vo vektoroch aj pre polep a tlač.",
                now: at),
            Offer.Create(
                LogoRedesignListing, MarekHruska, price: 190m, deliveryDays: 14,
                message: "Dobrý deň, rád vám logo zmodernizujem. Pripravím 2 návrhy a jedno kolo úprav, " +
                         "súbory dostanete v PNG a SVG.",
                now: at.AddHours(5)),
            Offer.Create(
                GoogleAdsListing, KlikMarketing, price: 300m, deliveryDays: 10,
                message: "Nastavíme vyhľadávacie kampane na pneuservis a opravy v okruhu 20 km od Košíc, " +
                         "meranie hovorov a prvý mesiac optimalizácie. Report každý týždeň.",
                now: at.AddDays(1)),
            Offer.Create(
                GoogleAdsListing, MarekHruska, price: 250m, deliveryDays: 7,
                message: "Spravím základnú kampaň v Google Ads s kľúčovými slovami pre váš servis.",
                now: at.AddDays(1).AddHours(3)));

        await db.SaveChangesAsync(cancellationToken);
    }
}
