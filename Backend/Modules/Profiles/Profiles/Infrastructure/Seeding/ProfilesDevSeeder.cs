using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Infrastructure.Seeding;

/// <summary>
/// Testovacie profily pre vývoj: 12 zadávateľov z testovacích inzerátov (ListingsDevSeeder),
/// takže karta zadávateľa na detaile inzerátu je hneď vyplnená.
/// ID MUSIA byť rovnaké ako ListingsDevSeeder.Owners – overuje to integračný test
/// Detail_SeededListing_HasOwnerFromProfiles. Spúšťa sa iba vo vývoji a iba do prázdnej tabuľky.
/// </summary>
internal sealed class ProfilesDevSeeder(ProfilesDbContext db) : IDevDataSeeder
{
    private static readonly DateTimeOffset SeededAt = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Profiles.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Profiles.AddRange(
            Company("019a0b2c-2000-7000-8000-000000000001", "Kaviareň Pod Hradom", "Bratislava", "Malá kaviareň v Starom Meste s domácimi koláčmi."),
            Person("019a0b2c-2000-7000-8000-000000000002", "Jana Kováčová", "Košice", "Vyrábam a predávam domácu prírodnú kozmetiku."),
            Company("019a0b2c-2000-7000-8000-000000000003", "Účtovníctvo Mráz s.r.o.", "Žilina", "Účtovná kancelária pre živnostníkov a malé firmy."),
            Company("019a0b2c-2000-7000-8000-000000000004", "AutoServis Kubica", "Nitra", "Rodinný autoservis – pneuservis, klimatizácie, STK."),
            Company("019a0b2c-2000-7000-8000-000000000005", "Salón Bella", "Banská Bystrica", "Kaderníctvo s tromi prevádzkami."),
            Person("019a0b2c-2000-7000-8000-000000000006", "Martin Hudák", "Prešov", "Rodinná včelárska farma, med zo Šariša."),
            Company("019a0b2c-2000-7000-8000-000000000007", "Stavby Trnava a.s.", "Trnava", "Stavebná firma – rodinné domy a rekonštrukcie."),
            Company("019a0b2c-2000-7000-8000-000000000008", "Pekáreň Zlatý klas", "Trenčín", "Remeselná pekáreň, 15 zamestnancov."),
            Company("019a0b2c-2000-7000-8000-000000000009", "Penzión Lesná", "Prešov", "Rodinný penzión vo Vysokých Tatrách."),
            Person("019a0b2c-2000-7000-8000-000000000010", "Zuzana Lehotská", "Bratislava", "Píšem knihy pre deti."),
            Company("019a0b2c-2000-7000-8000-000000000011", "FitNow s.r.o.", "Bratislava", "Startup s fitness aplikáciou."),
            Company("019a0b2c-2000-7000-8000-000000000012", "Kvetinárstvo Ruža", "Košice", "Kvetinárstvo a viazanie kytíc."));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Profile Company(string id, string name, string city, string bio) =>
        Profile.Create(Guid.Parse(id), ProfileType.Company, name, city, bio, skills: null, website: null, SeededAt);

    private static Profile Person(string id, string name, string city, string bio) =>
        Profile.Create(Guid.Parse(id), ProfileType.Person, name, city, bio, skills: null, website: null, SeededAt);
}
