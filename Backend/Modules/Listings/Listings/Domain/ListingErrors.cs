using Vybavim.Common.Errors;

namespace Vybavim.Modules.Listings.Domain;

/// <summary>
/// Všetky porušenia pravidiel inzerátu na jednom mieste: stabilný kód pre web a slovenská hláška pre ľudí.
/// Web môže podľa kódu zvýrazniť konkrétne pole formulára (napr. listing.title_too_long → pole Názov).
/// </summary>
internal static class ListingErrors
{
    public static readonly Error TitleRequired =
        Error.Validation("listing.title_required", "Názov inzerátu je povinný.");

    public static readonly Error TitleTooLong =
        Error.Validation("listing.title_too_long", $"Názov môže mať najviac {Listing.TitleMaxLength} znakov.");

    public static readonly Error DescriptionRequired =
        Error.Validation("listing.description_required", "Popis práce je povinný.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("listing.description_too_long", $"Popis môže mať najviac {Listing.DescriptionMaxLength} znakov.");

    public static readonly Error DurationRequired =
        Error.Validation("listing.duration_required", "Odhad trvania je povinný (napr. „2–4 týždne“).");

    public static readonly Error DurationTooLong =
        Error.Validation("listing.duration_too_long", $"Odhad trvania môže mať najviac {Listing.EstimatedDurationMaxLength} znakov.");

    public static readonly Error BudgetAmountNotAllowed =
        Error.Validation("listing.budget_amount_not_allowed", "Pri cene dohodou sa suma nezadáva.");

    public static readonly Error BudgetAmountRequired =
        Error.Validation("listing.budget_amount_required", "Pri pevnej cene a hodinovej sadzbe musí byť suma väčšia ako 0.");

    public static readonly Error CityRequired =
        Error.Validation("listing.city_required", "Ak práca nie je na diaľku, mesto je povinné.");

    public static readonly Error CityTooLong =
        Error.Validation("listing.city_too_long", $"Mesto môže mať najviac {Listing.CityMaxLength} znakov.");

    public static readonly Error DeadlineInPast =
        Error.Validation("listing.deadline_in_past", "Termín nesmie byť v minulosti.");

    public static readonly Error SkillsRequired =
        Error.Validation("listing.skills_required", "Inzerát musí mať aspoň jednu zručnosť.");

    public static readonly Error TooManyItems =
        Error.Validation("listing.too_many_items", $"Zoznam môže mať najviac {Listing.ListMaxItems} položiek.");

    public static readonly Error ItemTooLong =
        Error.Validation("listing.item_too_long", $"Položka zoznamu môže mať najviac {Listing.ListItemMaxLength} znakov.");

    public static readonly Error NotEditable =
        Error.Conflict("listing.not_editable", "Upraviť sa dá iba otvorený inzerát.");

    public static Error InvalidStatusChange(ListingStatus from, ListingStatus to) =>
        Error.Conflict("listing.invalid_status_change", $"Inzerát v stave {from} sa nedá prepnúť na {to}.");
}
