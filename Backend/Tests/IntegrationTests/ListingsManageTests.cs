using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vybavim.IntegrationTests;

/// <summary>
/// Pridanie, úprava, stiahnutie inzerátu a „moje inzeráty“ cez skutočné HTTP volania.
/// IClassFixture = táto trieda dostane vlastné Api s vlastnou databázou (WritableApiFactory).
/// </summary>
public sealed class ListingsManageTests(WritableApiFactory factory) : IClassFixture<WritableApiFactory>
{
    private const string Password = "Heslo1234";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Platný inzerát. Test zmení iba to, čo skúša (cez with { … } na anonymnom objekte to nejde, preto parametre).</summary>
    private static object ValidListing(string title = "Logo pre pekáreň", object? budget = null, string? deadline = null) => new
    {
        category = "grafika",
        title,
        description = "Hľadáme grafika na logo a vizitky.",
        budget = budget ?? new { type = "fixed", amount = 250 },
        city = "Nitra",
        remote = true,
        deadline,
        level = "intermediate",
        scope = new { size = "small", duration = "1–2 týždne" },
        engagement = "one_time",
        invoiceRequired = false,
        skills = new[] { "Logo", "Branding" },
        deliverables = new[] { "Logo vo vektoroch" },
        requirements = Array.Empty<string>(),
    };

    // --- Pomocné HTTP volania ---

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

    /// <summary>Zaregistruje nového používateľa a vráti jeho prístupový token.</summary>
    private async Task<string> NewUserTokenAsync()
    {
        var email = $"test-{Guid.NewGuid():N}@vybavim.test";
        await _client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), new { email, password = Password }, Ct);
        return await LoginAsync(email, Password);
    }

    private async Task<string> LoginAsync(string login, string password)
    {
        var response = await _client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { login, password }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    /// <summary>Pridá platný inzerát a vráti jeho ID.</summary>
    private async Task<string> CreateAsync(string token, string title = "Logo pre pekáreň")
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, ValidListing(title));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    // --- Pridanie ---

    [Fact]
    public async Task Create_WithoutLogin_Returns401()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token: null, ValidListing());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_Valid_Returns201AndListingIsPublic()
    {
        var token = await NewUserTokenAsync();
        var title = $"Logo {Guid.NewGuid():N}";

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, ValidListing(title));
        var created = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("open", created.GetProperty("status").GetString());
        Assert.Equal("grafika", created.GetProperty("category").GetString());
        Assert.Equal(response.Headers.Location!.ToString(), $"/api/v1/listings/{created.GetProperty("id").GetString()}");

        // Nový inzerát je hneď vo verejnom vyhľadávaní
        var search = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings?q={title}", token: null));
        Assert.Equal(1, search.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Create_OwnerIsTheLoggedInUser()
    {
        var token = await NewUserTokenAsync();
        var me = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/auth/me", token));

        var created = await JsonAsync(await SendAsync(HttpMethod.Post, "/api/v1/listings", token, ValidListing()));

        Assert.Equal(me.GetProperty("id").GetString(), created.GetProperty("ownerId").GetString());
    }

    [Fact]
    public async Task Create_NegotiableWithAmount_Returns400WithCode()
    {
        var token = await NewUserTokenAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token,
            ValidListing(budget: new { type = "negotiable", amount = 100 }));
        var problem = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("listing.budget_amount_not_allowed", problem.GetProperty("code").GetString());
        Assert.Equal("Pri cene dohodou sa suma nezadáva.", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Create_DeadlineInPast_Returns400()
    {
        var token = await NewUserTokenAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, ValidListing(deadline: "2020-01-01"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("listing.deadline_in_past", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_UnknownCategory_Returns400()
    {
        var token = await NewUserTokenAsync();
        var body = ValidListing();
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["category"] = "neexistuje";

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("listing.unknown_category", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_MissingRequiredField_Returns400()
    {
        var token = await NewUserTokenAsync();
        var json = JsonSerializer.SerializeToNode(ValidListing())!.AsObject();
        json.Remove("remote");   // povinné pole chýba – nesmie sa potichu dosadiť false

        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Úprava ---

    [Fact]
    public async Task Update_ByOwner_ChangesListing()
    {
        var token = await NewUserTokenAsync();
        var id = await CreateAsync(token);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/listings/{id}", token, ValidListing(title: "Upravený názov"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Upravený názov", (await JsonAsync(response)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Update_ByOtherUser_Returns403()
    {
        var ownerToken = await NewUserTokenAsync();
        var otherToken = await NewUserTokenAsync();
        var id = await CreateAsync(ownerToken);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/listings/{id}", otherToken, ValidListing(title: "Cudzí zásah"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("listing.not_owner", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Update_CancelledListing_Returns409()
    {
        var token = await NewUserTokenAsync();
        var id = await CreateAsync(token);
        await SendAsync(HttpMethod.Post, $"/api/v1/listings/{id}/cancel", token);

        var response = await SendAsync(HttpMethod.Put, $"/api/v1/listings/{id}", token, ValidListing());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("listing.not_editable", (await JsonAsync(response)).GetProperty("code").GetString());
    }

    // --- Stiahnutie ---

    [Fact]
    public async Task Cancel_ByOwner_HidesListingFromPublic()
    {
        var token = await NewUserTokenAsync();
        var title = $"Stiahnuť {Guid.NewGuid():N}";
        var id = await CreateAsync(token, title);

        var cancel = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{id}/cancel", token);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("cancelled", (await JsonAsync(cancel)).GetProperty("status").GetString());

        // Verejne: nie je vo vyhľadávaní a detail „neexistuje“
        var search = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings?q={title}", token: null));
        Assert.Equal(0, search.GetProperty("total").GetInt32());
        var publicDetail = await SendAsync(HttpMethod.Get, $"/api/v1/listings/{id}", token: null);
        Assert.Equal(HttpStatusCode.NotFound, publicDetail.StatusCode);

        // Autor ho stále vidí
        var ownerDetail = await SendAsync(HttpMethod.Get, $"/api/v1/listings/{id}", token);
        Assert.Equal(HttpStatusCode.OK, ownerDetail.StatusCode);
    }

    [Fact]
    public async Task Cancel_ByOtherUser_Returns403()
    {
        var id = await CreateAsync(await NewUserTokenAsync());

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{id}/cancel", await NewUserTokenAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_ByAdmin_Succeeds()
    {
        var id = await CreateAsync(await NewUserTokenAsync());
        var adminToken = await LoginAsync("Admin", "Admin123");

        // Admin môže stiahnuť cudzí inzerát (moderovanie)
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{id}/cancel", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Moje inzeráty ---

    [Fact]
    public async Task Mine_ReturnsOnlyOwnListingsIncludingCancelled()
    {
        var token = await NewUserTokenAsync();
        var first = await CreateAsync(token, "Prvý");
        await CreateAsync(token, "Druhý");
        await SendAsync(HttpMethod.Post, $"/api/v1/listings/{first}/cancel", token);
        await CreateAsync(await NewUserTokenAsync(), "Cudzí");   // iný používateľ

        var mine = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/listings/mine", token));
        var titles = mine.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();

        Assert.Equal(["Druhý", "Prvý"], titles);   // najnovšie prvé, aj stiahnutý, bez cudzieho
    }

    [Fact]
    public async Task Mine_WithoutLogin_Returns401()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/listings/mine", token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
