using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vybavim.IntegrationTests;

/// <summary>
/// Ponuky cez skutočné HTTP volania: poslanie, zoznamy, prijatie (Projects → Listings), odmietnutie, stiahnutie.
/// IClassFixture = vlastné Api s vlastnou databázou (WritableApiFactory), lebo testy pridávajú inzeráty.
/// </summary>
public sealed class OffersApiTests(WritableApiFactory factory) : IClassFixture<WritableApiFactory>
{
    private const string Password = "Heslo1234";

    private static readonly string[] Skills = ["Logo"];

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidOffer(decimal price = 280m) => new { price, deliveryDays = 14, message = "Robím logá 6 rokov, portfólio pošlem." };

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

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await JsonAsync(response)).GetProperty("code").GetString();

    /// <summary>Zaregistruje nového používateľa a vráti jeho prístupový token.</summary>
    private async Task<string> NewUserTokenAsync()
    {
        var email = $"test-{Guid.NewGuid():N}@vybavim.test";
        await _client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), new { email, password = Password }, Ct);
        var login = await _client.PostAsJsonAsync(new Uri("/api/v1/auth/login", UriKind.Relative), new { login = email, password = Password }, Ct);
        return (await JsonAsync(login)).GetProperty("accessToken").GetString()!;
    }

    /// <summary>Pridá platný inzerát a vráti jeho ID.</summary>
    private async Task<string> CreateListingAsync(string token)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/listings", token, new
        {
            category = "grafika",
            title = "Logo pre pekáreň",
            description = "Hľadáme grafika na logo.",
            budget = new { type = "fixed", amount = 300 },
            city = "Nitra",
            remote = true,
            deadline = (string?)null,
            level = "intermediate",
            scope = new { size = "small", duration = "1–2 týždne" },
            engagement = "one_time",
            invoiceRequired = false,
            skills = Skills,
            deliverables = Array.Empty<string>(),
            requirements = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    /// <summary>Pošle platnú ponuku a vráti jej ID.</summary>
    private async Task<string> SendOfferAsync(string token, string listingId)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", token, ValidOffer());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    // --- Poslanie ponuky ---

    [Fact]
    public async Task Send_WithoutLogin_Returns401()
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{Guid.NewGuid()}/offers", token: null, ValidOffer());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Send_Valid_OwnerSeesItWithProfile_FreelancerSeesItWithListingTitle()
    {
        var owner = await NewUserTokenAsync();
        var freelancer = await NewUserTokenAsync();
        var listingId = await CreateListingAsync(owner);
        await SendAsync(HttpMethod.Put, "/api/v1/profiles/me", freelancer,
            new { type = "person", displayName = "Jana Grafička", city = "Košice", bio = (string?)null, skills = (string[]?)null, website = (string?)null });

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", freelancer, ValidOffer(price: 280.50m));
        var sent = await JsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("pending", sent.GetProperty("status").GetString());
        Assert.Equal(280.50m, sent.GetProperty("price").GetDecimal());

        // Zadávateľ vidí ponuku aj s kartou odborníka (Projects → Profiles)
        var forOwner = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{listingId}/offers", owner));
        Assert.Equal("Jana Grafička", forOwner[0].GetProperty("freelancer").GetProperty("displayName").GetString());

        // Odborník vidí ponuku v „moje ponuky“ aj s názvom inzerátu (Projects → Listings)
        var mine = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/v1/offers/mine", freelancer));
        Assert.Equal("Logo pre pekáreň", mine[0].GetProperty("listingTitle").GetString());
    }

    [Fact]
    public async Task Send_OnOwnListing_Returns403()
    {
        var owner = await NewUserTokenAsync();
        var listingId = await CreateListingAsync(owner);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", owner, ValidOffer());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("offer.own_listing", await CodeAsync(response));
    }

    [Fact]
    public async Task Send_UnknownListing_Returns404()
    {
        var freelancer = await NewUserTokenAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{Guid.NewGuid()}/offers", freelancer, ValidOffer());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("listing.not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task Send_InvalidPrice_Returns400WithCode()
    {
        var listingId = await CreateListingAsync(await NewUserTokenAsync());

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", await NewUserTokenAsync(), ValidOffer(price: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("offer.invalid_price", await CodeAsync(response));
    }

    [Fact]
    public async Task Send_Twice_Returns409_AfterWithdrawCanSendAgain()
    {
        var listingId = await CreateListingAsync(await NewUserTokenAsync());
        var freelancer = await NewUserTokenAsync();
        var offerId = await SendOfferAsync(freelancer, listingId);

        var second = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", freelancer, ValidOffer());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("offer.already_sent", await CodeAsync(second));

        var withdrawn = await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/withdraw", freelancer);
        Assert.Equal("withdrawn", (await JsonAsync(withdrawn)).GetProperty("status").GetString());

        var again = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", freelancer, ValidOffer());
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task Send_OnCancelledListing_Returns409()
    {
        var owner = await NewUserTokenAsync();
        var listingId = await CreateListingAsync(owner);
        await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/cancel", owner);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", await NewUserTokenAsync(), ValidOffer());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("offer.listing_not_open", await CodeAsync(response));
    }

    // --- Kto čo vidí a smie ---

    [Fact]
    public async Task ListingOffers_ForSomeoneElse_Returns403()
    {
        var listingId = await CreateListingAsync(await NewUserTokenAsync());

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/listings/{listingId}/offers", await NewUserTokenAsync());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("offer.not_listing_owner", await CodeAsync(response));
    }

    [Fact]
    public async Task AcceptAndReject_BySomeoneElse_Return403_WithdrawByOwner_Returns403()
    {
        var owner = await NewUserTokenAsync();
        var freelancer = await NewUserTokenAsync();
        var stranger = await NewUserTokenAsync();
        var offerId = await SendOfferAsync(freelancer, await CreateListingAsync(owner));

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/accept", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/accept", freelancer)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/reject", stranger)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/withdraw", owner)).StatusCode);
    }

    // --- Prijatie: proces cez dva moduly ---

    [Fact]
    public async Task Accept_RejectsOtherOffers_AndListingGoesInProgress()
    {
        var owner = await NewUserTokenAsync();
        var listingId = await CreateListingAsync(owner);
        var chosen = await SendOfferAsync(await NewUserTokenAsync(), listingId);
        var other = await SendOfferAsync(await NewUserTokenAsync(), listingId);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/offers/{chosen}/accept", owner);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("accepted", (await JsonAsync(response)).GetProperty("status").GetString());

        // Ostatné čakajúce ponuky sú odmietnuté
        var offers = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{listingId}/offers", owner));
        var otherOffer = offers.EnumerateArray().Single(o => o.GetProperty("id").GetString() == other);
        Assert.Equal("rejected", otherOffer.GetProperty("status").GetString());

        // Inzerát v module Listings prešiel do stavu „pracuje sa“ a nové ponuky neprijíma
        var listing = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{listingId}", owner));
        Assert.Equal("in_progress", listing.GetProperty("status").GetString());

        var late = await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/offers", await NewUserTokenAsync(), ValidOffer());
        Assert.Equal("offer.listing_not_open", await CodeAsync(late));
    }

    [Fact]
    public async Task Accept_OnCancelledListing_Returns409_AndOfferStaysPending()
    {
        var owner = await NewUserTokenAsync();
        var listingId = await CreateListingAsync(owner);
        var offerId = await SendOfferAsync(await NewUserTokenAsync(), listingId);
        await SendAsync(HttpMethod.Post, $"/api/v1/listings/{listingId}/cancel", owner);

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/accept", owner);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("offer.listing_not_open", await CodeAsync(response));

        // Transakcia sa zrušila – ponuka ostala čakajúca, nie „prijatá“ bez inzerátu
        var offers = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/v1/listings/{listingId}/offers", owner));
        Assert.Equal("pending", offers[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reject_ThenAccept_Returns409()
    {
        var owner = await NewUserTokenAsync();
        var offerId = await SendOfferAsync(await NewUserTokenAsync(), await CreateListingAsync(owner));

        var rejected = await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/reject", owner);
        Assert.Equal("rejected", (await JsonAsync(rejected)).GetProperty("status").GetString());

        var accept = await SendAsync(HttpMethod.Post, $"/api/v1/offers/{offerId}/accept", owner);
        Assert.Equal(HttpStatusCode.Conflict, accept.StatusCode);
        Assert.Equal("offer.invalid_status_change", await CodeAsync(accept));
    }
}
