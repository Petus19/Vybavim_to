using Vybavim.Common.Errors;

namespace Vybavim.Modules.Profiles.Domain;

/// <summary>Porušenia pravidiel profilu: stabilný kód pre web a slovenská hláška pre ľudí.</summary>
internal static class ProfileErrors
{
    public static readonly Error DisplayNameRequired =
        Error.Validation("profile.display_name_required", "Meno (alebo názov firmy) je povinné.");

    public static readonly Error DisplayNameTooLong =
        Error.Validation("profile.display_name_too_long", $"Meno môže mať najviac {Profile.DisplayNameMaxLength} znakov.");

    public static readonly Error CityTooLong =
        Error.Validation("profile.city_too_long", $"Mesto môže mať najviac {Profile.CityMaxLength} znakov.");

    public static readonly Error BioTooLong =
        Error.Validation("profile.bio_too_long", $"Popis môže mať najviac {Profile.BioMaxLength} znakov.");

    public static readonly Error TooManySkills =
        Error.Validation("profile.too_many_skills", $"Zručností môže byť najviac {Profile.SkillsMaxItems}.");

    public static readonly Error SkillTooLong =
        Error.Validation("profile.skill_too_long", $"Zručnosť môže mať najviac {Profile.SkillMaxLength} znakov.");

    public static readonly Error InvalidWebsite =
        Error.Validation("profile.invalid_website", "Web musí byť celá adresa začínajúca http:// alebo https://.");
}
