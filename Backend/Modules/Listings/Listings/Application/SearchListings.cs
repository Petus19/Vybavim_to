using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Errors;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>
/// Parametre vyhľadávania z URL, napr. /api/v1/listings?q=logo&amp;category=grafika&amp;sort=cheapest.
/// Všetky sú nepovinné. Pravidlá sú rovnaké ako na webe (Frontend/Web/src/lib/listings.ts).
/// </summary>
internal sealed class ListingSearchQuery
{
    /// <summary>Hľadaný text v názve, popise a zručnostiach (bez ohľadu na diakritiku a veľké písmená).</summary>
    [FromQuery(Name = "q")] public string? Q { get; init; }

    /// <summary>Slug kategórie: web, grafika, it-podpora, marketing.</summary>
    [FromQuery(Name = "category")] public string? Category { get; init; }

    [FromQuery(Name = "city")] public string? City { get; init; }

    /// <summary>fixed, hourly, negotiable.</summary>
    [FromQuery(Name = "budgetType")] public string? BudgetType { get; init; }

    /// <summary>Rozsah pevnej ceny v € (porovnáva sa iba s pevnou cenou).</summary>
    [FromQuery(Name = "priceMin")] public decimal? PriceMin { get; init; }

    [FromQuery(Name = "priceMax")] public decimal? PriceMax { get; init; }

    /// <summary>Rozsah hodinovej sadzby v €/hod. (porovnáva sa iba so sadzbou).</summary>
    [FromQuery(Name = "rateMin")] public decimal? RateMin { get; init; }

    [FromQuery(Name = "rateMax")] public decimal? RateMax { get; init; }

    /// <summary>beginner, intermediate, expert.</summary>
    [FromQuery(Name = "level")] public string? Level { get; init; }

    /// <summary>one_time, ongoing.</summary>
    [FromQuery(Name = "engagement")] public string? Engagement { get; init; }

    /// <summary>true = iba práca na diaľku.</summary>
    [FromQuery(Name = "remote")] public bool? Remote { get; init; }

    /// <summary>true = iba otvorené inzeráty.</summary>
    [FromQuery(Name = "openOnly")] public bool? OpenOnly { get; init; }

    /// <summary>true = iba inzeráty, ktoré nevyžadujú faktúru.</summary>
    [FromQuery(Name = "noInvoice")] public bool? NoInvoice { get; init; }

    /// <summary>newest (predvolené), cheapest, priciest.</summary>
    [FromQuery(Name = "sort")] public string? Sort { get; init; }

    /// <summary>Číslo stránky od 1 (predvolené 1).</summary>
    [FromQuery(Name = "page")] public int? Page { get; init; }

    /// <summary>Počet inzerátov na stránku, 1–50 (predvolené 20).</summary>
    [FromQuery(Name = "pageSize")] public int? PageSize { get; init; }
}

/// <summary>Prípad použitia: „vyhľadaj inzeráty podľa filtrov“.</summary>
internal sealed class SearchListings(ListingsDbContext db)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public async Task<Result<PagedResponse<ListingSummaryResponse>>> HandleAsync(
        ListingSearchQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Overenie a prevod parametrov z textu na enumy ("one_time" → Engagement.OneTime)
        if (!TryParseEnum<BudgetType>(query.BudgetType, out var budgetType)
            || !TryParseEnum<ExperienceLevel>(query.Level, out var level)
            || !TryParseEnum<Engagement>(query.Engagement, out var engagement))
        {
            return Fail("listing.invalid_filter", "Neplatná hodnota filtra (budgetType, level alebo engagement).");
        }

        var sort = query.Sort?.Trim().ToLowerInvariant() ?? "newest";
        if (sort is not ("newest" or "cheapest" or "priciest"))
        {
            return Fail("listing.invalid_sort", "Zoradenie môže byť newest, cheapest alebo priciest.");
        }

        if (!IsValidRange(query.PriceMin, query.PriceMax) || !IsValidRange(query.RateMin, query.RateMax))
        {
            return Fail("listing.invalid_range", "Cena musí byť kladná a „od“ nesmie byť väčšie ako „do“.");
        }

        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? DefaultPageSize;
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            return Fail("listing.invalid_page", $"Stránka musí byť aspoň 1 a veľkosť stránky 1 až {MaxPageSize}.");
        }

        // 2. Zostavenie dotazu. Nič sa ešte nespúšťa – EF Core skladá SQL, kým nezavoláme
        //    CountAsync/ToListAsync. Každý Where pridá ďalšiu podmienku do WHERE.
        var listings = db.Listings
            .AsNoTracking()
            .Where(l => l.Status != ListingStatus.Cancelled)   // stiahnuté inzeráty verejne nezobrazujeme
            .Join(db.Categories, l => l.CategoryId, c => c.Id, (l, c) => new { Listing = l, CategorySlug = c.Slug });

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // unaccent() odstráni diakritiku na oboch stranách, ILIKE ignoruje veľké písmená:
            // „kaviaren“ nájde „Kaviareň“. Znaky % a _ od používateľa escapujeme.
            var pattern = $"%{EscapeLikePattern(query.Q.Trim())}%";
            listings = listings.Where(x => EF.Functions.ILike(
                EF.Functions.Unaccent(x.Listing.Title + " " + x.Listing.Description + " " + string.Join(" ", x.Listing.Skills)),
                EF.Functions.Unaccent(pattern),
                "\\"));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            listings = listings.Where(x => x.CategorySlug == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            listings = listings.Where(x => x.Listing.City == query.City);
        }

        if (budgetType is not null)
        {
            listings = listings.Where(x => x.Listing.BudgetType == budgetType);
        }

        // Ceny: pevná cena sa porovnáva iba s rozsahom pevnej ceny a sadzba iba s rozsahom sadzby.
        // Ak je zadaný aspoň jeden rozsah, inzeráty „dohodou“ sa skryjú (nemajú sumu).
        var hasPriceRange = query.PriceMin is not null || query.PriceMax is not null;
        var hasRateRange = query.RateMin is not null || query.RateMax is not null;
        if (hasPriceRange || hasRateRange)
        {
            listings = listings.Where(x =>
                (hasPriceRange
                    && x.Listing.BudgetType == BudgetType.Fixed
                    && (query.PriceMin == null || x.Listing.BudgetAmount >= query.PriceMin)
                    && (query.PriceMax == null || x.Listing.BudgetAmount <= query.PriceMax))
                || (hasRateRange
                    && x.Listing.BudgetType == BudgetType.Hourly
                    && (query.RateMin == null || x.Listing.BudgetAmount >= query.RateMin)
                    && (query.RateMax == null || x.Listing.BudgetAmount <= query.RateMax)));
        }

        if (level is not null)
        {
            listings = listings.Where(x => x.Listing.Level == level);
        }

        if (engagement is not null)
        {
            listings = listings.Where(x => x.Listing.Engagement == engagement);
        }

        if (query.Remote == true)
        {
            listings = listings.Where(x => x.Listing.Remote);
        }

        if (query.OpenOnly == true)
        {
            listings = listings.Where(x => x.Listing.Status == ListingStatus.Open);
        }

        if (query.NoInvoice == true)
        {
            listings = listings.Where(x => !x.Listing.InvoiceRequired);
        }

        // 3. Zoradenie. Pri cene: najprv pevné ceny, potom sadzby, nakoniec „dohodou“ –
        //    rôzne typy cien sa navzájom neporovnávajú (rovnako ako na webe).
        listings = sort switch
        {
            "cheapest" => listings
                .OrderBy(x => x.Listing.BudgetType == BudgetType.Fixed ? 0 : x.Listing.BudgetType == BudgetType.Hourly ? 1 : 2)
                .ThenBy(x => x.Listing.BudgetAmount)
                .ThenByDescending(x => x.Listing.CreatedAt),
            "priciest" => listings
                .OrderBy(x => x.Listing.BudgetType == BudgetType.Fixed ? 0 : x.Listing.BudgetType == BudgetType.Hourly ? 1 : 2)
                .ThenByDescending(x => x.Listing.BudgetAmount)
                .ThenByDescending(x => x.Listing.CreatedAt),
            _ => listings.OrderByDescending(x => x.Listing.CreatedAt),
        };

        // 4. Spustenie: počet všetkých výsledkov + jedna stránka
        var total = await listings.CountAsync(cancellationToken);

        var items = await listings
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ListingSummaryResponse(
                x.Listing.Id,
                x.Listing.Title,
                x.CategorySlug,
                x.Listing.City,
                x.Listing.Remote,
                new BudgetResponse(x.Listing.BudgetType, x.Listing.BudgetAmount),
                x.Listing.Status,
                x.Listing.Level,
                x.Listing.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResponse<ListingSummaryResponse>(items, total, page, pageSize));
    }

    private static Result<PagedResponse<ListingSummaryResponse>> Fail(string code, string message) =>
        Result.Failure<PagedResponse<ListingSummaryResponse>>(Error.Validation(code, message));

    /// <summary>
    /// Prevedie text z URL na enum: "one_time" → OneTime, "fixed" → Fixed.
    /// Prázdny text = filter nie je zadaný (result = null). Neznámy text = false.
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? value, out TEnum? result)
        where TEnum : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var name = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (!int.TryParse(name, out _) && Enum.TryParse<TEnum>(name, ignoreCase: true, out var parsed))
        {
            result = parsed;
            return true;
        }

        return false;
    }

    private static bool IsValidRange(decimal? min, decimal? max) =>
        min is not < 0 && max is not < 0 && (min is null || max is null || min <= max);

    /// <summary>Znaky \ % _ majú v LIKE špeciálny význam – používateľov text ich musí brať doslova.</summary>
    private static string EscapeLikePattern(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
