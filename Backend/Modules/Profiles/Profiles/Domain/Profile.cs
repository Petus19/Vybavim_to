using Vybavim.Common.Errors;
using Vybavim.Modules.Profiles.Contracts;

namespace Vybavim.Modules.Profiles.Domain;

/// <summary>
/// Verejný profil používateľa: kto je (jednotlivec / firma), odkiaľ je a čím sa zaoberá.
/// Každý používateľ má najviac jeden profil – kľúčom je priamo ID používateľa z modulu Identity.
/// Prihlasovacie údaje (e-mail, heslo) sem nepatria, tie sú v Identity.
/// </summary>
internal sealed class Profile
{
    public const int DisplayNameMaxLength = 100;
    public const int CityMaxLength = 100;
    public const int BioMaxLength = 2000;
    public const int WebsiteMaxLength = 200;
    public const int SkillsMaxItems = 20;
    public const int SkillMaxLength = 50;

    // Private konštruktor: profil vznikne iba cez Create(). EF Core ho použije pri načítaní z DB.
    private Profile(
        Guid userId,
        ProfileType type,
        string displayName,
        string? city,
        string? bio,
        string[] skills,
        string? website,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        Type = type;
        DisplayName = displayName;
        City = city;
        Bio = bio;
        Skills = skills;
        Website = website;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>ID používateľa z modulu Identity – zároveň ID profilu (jeden používateľ = jeden profil).</summary>
    public Guid UserId { get; private set; }

    public ProfileType Type { get; private set; }

    /// <summary>Meno jednotlivca alebo názov firmy, napr. "Jana Kováčová" / "Kaviareň Pod Hradom".</summary>
    public string DisplayName { get; private set; }

    public string? City { get; private set; }

    /// <summary>Krátky popis: čím sa zaoberám, prax, čo ponúkam.</summary>
    public string? Bio { get; private set; }

    /// <summary>Zručnosti odborníka, napr. ["Branding", "Illustrator"]. Zadávateľ ich mať nemusí.</summary>
    public string[] Skills { get; private set; }

    /// <summary>Web alebo portfólio, napr. "https://jana-grafika.sk".</summary>
    public string? Website { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Vytvorí profil. Pravidlá overí <see cref="Normalize"/> – rovnaké ako pri úprave.</summary>
    public static Profile Create(
        Guid userId,
        ProfileType type,
        string displayName,
        string? city,
        string? bio,
        IEnumerable<string>? skills,
        string? website,
        DateTimeOffset now)
    {
        var f = Normalize(displayName, city, bio, skills, website);
        return new Profile(userId, type, f.DisplayName, f.City, f.Bio, f.Skills, f.Website, now, now);
    }

    /// <summary>Upraví profil. Pri porušení pravidla ostane profil celý pôvodný.</summary>
    public void Update(
        ProfileType type,
        string displayName,
        string? city,
        string? bio,
        IEnumerable<string>? skills,
        string? website,
        DateTimeOffset now)
    {
        var f = Normalize(displayName, city, bio, skills, website);

        Type = type;
        DisplayName = f.DisplayName;
        City = f.City;
        Bio = f.Bio;
        Skills = f.Skills;
        Website = f.Website;
        UpdatedAt = now;
    }

    private readonly record struct EditableFields(string DisplayName, string? City, string? Bio, string[] Skills, string? Website);

    /// <summary>Všetky pravidlá profilu na jednom mieste. Pri porušení hodí DomainException.</summary>
    private static EditableFields Normalize(
        string displayName,
        string? city,
        string? bio,
        IEnumerable<string>? skills,
        string? website)
    {
        var cleanName = displayName?.Trim() ?? string.Empty;
        if (cleanName.Length == 0)
        {
            throw new DomainException(ProfileErrors.DisplayNameRequired);
        }

        if (cleanName.Length > DisplayNameMaxLength)
        {
            throw new DomainException(ProfileErrors.DisplayNameTooLong);
        }

        var cleanCity = Optional(city, CityMaxLength, ProfileErrors.CityTooLong);
        var cleanBio = Optional(bio, BioMaxLength, ProfileErrors.BioTooLong);
        var cleanWebsite = Optional(website, WebsiteMaxLength, ProfileErrors.InvalidWebsite);

        // Web: iba celá adresa http(s) – nie "javascript:…" ani nič, čo by sa dalo zneužiť v odkaze
        if (cleanWebsite is not null
            && !(Uri.TryCreate(cleanWebsite, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            throw new DomainException(ProfileErrors.InvalidWebsite);
        }

        var cleanSkills = (skills ?? [])
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cleanSkills.Length > SkillsMaxItems)
        {
            throw new DomainException(ProfileErrors.TooManySkills);
        }

        if (cleanSkills.Any(s => s.Length > SkillMaxLength))
        {
            throw new DomainException(ProfileErrors.SkillTooLong);
        }

        return new EditableFields(cleanName, cleanCity, cleanBio, cleanSkills, cleanWebsite);
    }

    /// <summary>Nepovinný text: orezaný; prázdny = null; dlhší ako maxLength = chyba.</summary>
    private static string? Optional(string? value, int maxLength, Error whenTooLong)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return null;
        }

        return clean.Length <= maxLength ? clean : throw new DomainException(whenTooLong);
    }
}
