using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Vybavim.IntegrationTests;

/// <summary>Api so zapnutými demo dátami (DevData:DemoAccounts) a vlastnou databázou.</summary>
public sealed class DemoApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("DevData:DemoAccounts", "true");
    }
}

/// <summary>
/// Demo dáta pre AI agenta: štyri moduly ich vkladajú každý zvlášť a odkazujú na seba iba cez ID
/// (účet → profil → inzerát → ponuka). Test overuje, že do seba zapadajú – cez verejné API, ako ich uvidí agent.
/// </summary>
public sealed class DemoDataTests(DemoApiFactory factory) : IClassFixture<DemoApiFactory>
{
    private const string LogoRedesignListing = "019a0b2c-4000-7000-8000-000000000001";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<string> LoginAsync(string userName)
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { login = userName, password = "Demo1234" }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accessToken").GetString()!;
    }

    private async Task<JsonElement> GetAsync(string url, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task Search_ContainsOriginalAndDemoListings()
    {
        var page = await GetAsync("/api/v1/listings?pageSize=1");

        Assert.Equal(61, page.GetProperty("total").GetInt32());   // 12 pôvodných + 49 demo
    }

    [Fact]
    public async Task DemoClient_OwnsThreeListings()
    {
        var token = await LoginAsync("client.demo");

        var mine = await GetAsync("/api/v1/listings/mine", token);

        Assert.Equal(3, mine.GetProperty("total").GetInt32());
        Assert.Contains(
            mine.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetString() == LogoRedesignListing);
    }

    [Fact]
    public async Task DemoClient_SeesCompetingOffersWithSpecialistProfiles()
    {
        var token = await LoginAsync("client.demo");

        var offers = await GetAsync($"/api/v1/listings/{LogoRedesignListing}/offers", token);

        var names = offers.EnumerateArray()
            .Select(o => o.GetProperty("freelancer").GetProperty("displayName").GetString())
            .Order()
            .ToList();
        Assert.Equal(["Lucia Bartošová", "Marek Hruška"], names);
    }

    [Fact]
    public async Task DemoListing_HasOwnerProfile()
    {
        var detail = await GetAsync($"/api/v1/listings/{LogoRedesignListing}");

        Assert.Equal("Autoservis Novák", detail.GetProperty("owner").GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("specialist.demo", 1)]
    [InlineData("specialist2.demo", 2)]
    [InlineData("specialist3.demo", 1)]
    public async Task DemoSpecialists_SeeTheirOffers(string userName, int expected)
    {
        var token = await LoginAsync(userName);

        var offers = await GetAsync("/api/v1/offers/mine", token);

        Assert.Equal(expected, offers.GetArrayLength());
    }
}
