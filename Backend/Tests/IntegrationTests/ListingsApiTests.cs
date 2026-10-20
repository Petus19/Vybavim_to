using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vybavim.IntegrationTests;

/// <summary>
/// Testy endpointov modulu Listings cez skutočné HTTP volania.
/// Dáta pochádzajú z ListingsDevSeeder (12 inzerátov zhodných s webom).
/// </summary>
public sealed class ListingsApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Zavolá GET a vráti telo odpovede ako JSON (overí, že prišlo 200 OK).</summary>
    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(new Uri(url, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static List<string> Titles(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!).ToList();

    // --- Kategórie ---

    [Fact]
    public async Task Categories_ReturnsFourMainCategoriesInOrder()
    {
        var categories = await GetJsonAsync("/api/v1/categories");

        var slugs = categories.EnumerateArray().Select(c => c.GetProperty("slug").GetString()).ToList();
        Assert.Equal(["web", "grafika", "it-podpora", "marketing"], slugs);
    }

    // --- Zoznam a filtre ---

    [Fact]
    public async Task Listings_WithoutFilters_ReturnsAllNewestFirst()
    {
        var page = await GetJsonAsync("/api/v1/listings");

        Assert.Equal(12, page.GetProperty("total").GetInt32());
        Assert.Equal("Logo a vizuálna identita pre kaviareň", Titles(page)[0]);
    }

    [Theory]
    [InlineData("kaviaren")]   // bez diakritiky
    [InlineData("KAVIAREŇ")]   // veľkými písmenami s diakritikou
    public async Task Listings_SearchIgnoresDiacriticsAndCase(string query)
    {
        var page = await GetJsonAsync($"/api/v1/listings?q={Uri.EscapeDataString(query)}");

        Assert.Equal(["Logo a vizuálna identita pre kaviareň"], Titles(page));
    }

    [Fact]
    public async Task Listings_SearchFindsSkills()
    {
        // "Shoptet" je v zručnostiach inzerátu
        var page = await GetJsonAsync("/api/v1/listings?q=shoptet");

        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listings_SearchTreatsPercentLiterally()
    {
        // % nesmie znamenať „čokoľvek“ – inak by sa našli všetky inzeráty
        var page = await GetJsonAsync("/api/v1/listings?q=50%25");

        Assert.Equal(0, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listings_PriceRange_ReturnsOnlyFixedPrices()
    {
        var page = await GetJsonAsync("/api/v1/listings?priceMin=500");

        // Pevná cena sa nikdy nemieša s hodinovou sadzbou ani s „dohodou“
        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            var budget = item.GetProperty("budget");
            Assert.Equal("fixed", budget.GetProperty("type").GetString());
            Assert.True(budget.GetProperty("amount").GetDecimal() >= 500);
        }

        Assert.Equal(5, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listings_RateRange_ReturnsOnlyHourlyRates()
    {
        var page = await GetJsonAsync("/api/v1/listings?rateMax=26");

        Assert.Equal(["Správa siete a počítačov v malej kancelárii"], Titles(page));
    }

    [Fact]
    public async Task Listings_SortCheapest_FixedFirstNegotiableLast()
    {
        var page = await GetJsonAsync("/api/v1/listings?sort=cheapest");
        var types = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("budget").GetProperty("type").GetString())
            .ToList();

        Assert.Equal("Grafika na obaly lokálneho medu", Titles(page)[0]);   // najlacnejšia pevná cena (250 €)
        Assert.Equal("fixed", types[0]);
        Assert.Equal("negotiable", types[^1]);                              // „dohodou“ na konci
    }

    [Fact]
    public async Task Listings_Paging_ReturnsRequestedPage()
    {
        var page = await GetJsonAsync("/api/v1/listings?page=3&pageSize=5");

        Assert.Equal(12, page.GetProperty("total").GetInt32());
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());   // 5 + 5 + 2
    }

    [Fact]
    public async Task Listings_InvalidFilter_Returns400WithCode()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/listings?level=guru", UriKind.Relative), Ct);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("listing.invalid_filter", problem.GetProperty("code").GetString());
    }

    // --- Detail ---

    [Fact]
    public async Task Detail_ExistingListing_ReturnsAllFields()
    {
        var search = await GetJsonAsync("/api/v1/listings?q=kaviaren");
        var id = search.GetProperty("items")[0].GetProperty("id").GetString();

        var detail = await GetJsonAsync($"/api/v1/listings/{id}");

        Assert.Equal("grafika", detail.GetProperty("category").GetString());
        Assert.Equal("open", detail.GetProperty("status").GetString());
        Assert.Equal("one_time", detail.GetProperty("engagement").GetString());
        Assert.Equal("2026-11-15", detail.GetProperty("deadline").GetString());
        Assert.Contains("Branding", detail.GetProperty("skills").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task Detail_UnknownListing_Returns404WithCode()
    {
        var response = await _client.GetAsync(new Uri($"/api/v1/listings/{Guid.Empty}", UriKind.Relative), Ct);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("listing.not_found", problem.GetProperty("code").GetString());
    }
}
