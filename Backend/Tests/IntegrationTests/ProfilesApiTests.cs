using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vybavim.IntegrationTests;

/// <summary>Môj profil, verejný profil a karta autora na detaile inzerátu (Listings → Profiles.Contracts).</summary>
public sealed class ProfilesApiTests(ApiFactory factory)
{
    private const string Password = "Heslo1234";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidProfile(string displayName = "Jana Grafička", string? website = null) => new
    {
        type = "person",
        displayName,
        city = "Košice",
        bio = "Grafička s 5-ročnou praxou.",
        skills = new[] { "Branding", "Illustrator" },
        website,
    };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<string> NewUserTokenAsync()
    {
        var email = $"test-{Guid.NewGuid():N}@vybavim.test";
        await _client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), new { email, password = Password }, Ct);
        var login = await _client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { login = email, password = Password }, Ct);
        return (await login.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    // --- Môj profil ---

    [Fact]
    public async Task MyProfile_BeforeSaving_Returns404()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/profiles/me", await NewUserTokenAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("profile.not_found", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task SaveMyProfile_CreatesThenUpdates()
    {
        var token = await NewUserTokenAsync();

        var created = await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", token, ValidProfile("Jana Grafička"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var updated = await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", token, ValidProfile("Jana – grafické štúdio"));
        var me = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/profiles/me", token));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Jana – grafické štúdio", me.GetProperty("displayName").GetString());
        Assert.Equal("person", me.GetProperty("type").GetString());
        Assert.Equal(["Branding", "Illustrator"], me.GetProperty("skills").EnumerateArray().Select(s => s.GetString()));
    }

    [Fact]
    public async Task SaveMyProfile_InvalidWebsite_Returns400WithCode()
    {
        var response = await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", await NewUserTokenAsync(),
            ValidProfile(website: "javascript:alert(1)"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("profile.invalid_website", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task SaveMyProfile_WithoutLogin_Returns401()
    {
        var response = await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", token: null, ValidProfile());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Verejný profil ---

    [Fact]
    public async Task PublicProfile_IsVisibleWithoutLogin()
    {
        var token = await NewUserTokenAsync();
        var saved = await JsonAsync(await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", token, ValidProfile("Verejná Jana")));
        var userId = saved.GetProperty("userId").GetString();

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/profiles/{userId}", token: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Verejná Jana", (await JsonAsync(response)).GetProperty("displayName").GetString());
    }

    // --- Prepojenie s inzerátmi ---

    [Fact]
    public async Task ListingDetail_SeededListing_HasOwnerFromProfiles()
    {
        // Testovacie ID zadávateľov v ProfilesDevSeeder musia sedieť s ListingsDevSeeder
        var search = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/listings?q=kaviaren", token: null));
        var id = search.GetProperty("items")[0].GetProperty("id").GetString();

        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{id}", token: null));
        var owner = detail.GetProperty("owner");

        Assert.Equal("Kaviareň Pod Hradom", owner.GetProperty("displayName").GetString());
        Assert.Equal("company", owner.GetProperty("type").GetString());
        Assert.Equal(detail.GetProperty("ownerId").GetString(), owner.GetProperty("id").GetString());
    }

    [Fact]
    public async Task ListingDetail_AllSeededListingsHaveOwner()
    {
        var page = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/listings?pageSize=50", token: null));

        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{item.GetProperty("id").GetString()}", token: null));
            Assert.Equal(JsonValueKind.Object, detail.GetProperty("owner").ValueKind);
        }
    }
}
