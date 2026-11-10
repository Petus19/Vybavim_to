using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Infrastructure.Seeding;

/// <summary>
/// Profily demo účtov (IdentityDemoSeeder) – zadávateľ a traja špecialisti s rôznymi zručnosťami,
/// aby mal agent pri porovnávaní ponúk čo porovnávať.
/// Registruje sa iba vo vývoji so zapnutým <see cref="DevData.DemoAccountsKey"/>.
/// </summary>
internal sealed class ProfilesDemoSeeder(ProfilesDbContext db) : IDevDataSeeder
{
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var profiles = new[]
        {
            Profile.Create(
                Guid.Parse("019a0b2c-3000-7000-8000-000000000001"), ProfileType.Company, "Autoservis Novák", "Košice",
                "Rodinný autoservis na Juhu – opravy, pneuservis a diagnostika. Chceme sa viac ukázať online.",
                skills: null, website: null, SeededAt),
            Profile.Create(
                Guid.Parse("019a0b2c-3000-7000-8000-000000000002"), ProfileType.Person, "Lucia Bartošová", "Košice",
                "Grafická dizajnérka, 8 rokov praxe. Logá a vizuálne identity pre malé firmy, 60+ hotových značiek.",
                skills: ["Logo dizajn", "Branding", "Adobe Illustrator", "Typografia"], website: null, SeededAt),
            Profile.Create(
                Guid.Parse("019a0b2c-3000-7000-8000-000000000003"), ProfileType.Person, "Marek Hruška", "Prešov",
                "Začínajúci webový a grafický dizajnér, rok na voľnej nohe. Rýchla komunikácia, priaznivé ceny.",
                skills: ["Webdizajn", "Figma", "Canva", "WordPress", "Google Ads"], website: null, SeededAt),
            Profile.Create(
                Guid.Parse("019a0b2c-3000-7000-8000-000000000004"), ProfileType.Company, "Klik Marketing s.r.o.",
                "Bratislava",
                "Malá agentúra pre výkonnostný marketing – Google Ads, Meta Ads a meranie konverzií pre e-shopy a služby.",
                skills: ["Google Ads", "Meta Ads", "Google Analytics 4", "PPC"], website: null, SeededAt),
        };

        var existing = await db.Profiles.Select(p => p.UserId).ToListAsync(cancellationToken);
        db.Profiles.AddRange(profiles.Where(p => !existing.Contains(p.UserId)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
