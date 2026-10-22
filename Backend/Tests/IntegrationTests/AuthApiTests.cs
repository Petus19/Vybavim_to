using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vybavim.IntegrationTests;

/// <summary>
/// Testy registrácie, prihlásenia a oprávnení cez skutočné HTTP volania.
/// Admin (Admin / Admin123) pochádza z IdentityDevSeeder.
/// </summary>
public sealed class AuthApiTests(ApiFactory factory)
{
    private const string AdminLogin = "Admin";
    private const string AdminPassword = "Admin123";
    private const string ValidPassword = "Heslo1234";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Jedinečný e-mail pre každý test, aby sa testy navzájom neovplyvňovali.</summary>
    private static string NewEmail() => $"test-{Guid.NewGuid():N}@vybavim.test";

    private Task<HttpResponseMessage> PostAsync(string url, object body) =>
        _client.PostAsJsonAsync(new Uri(url, UriKind.Relative), body, Ct);

    /// <summary>GET s prístupovým tokenom v hlavičke "Authorization: Bearer …" (alebo bez neho).</summary>
    private Task<HttpResponseMessage> GetAsync(string url, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> LoginAsync(string login, string password)
    {
        var response = await PostAsync("/api/v1/auth/login", new { login, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static string AccessToken(JsonElement tokens) => tokens.GetProperty("accessToken").GetString()!;

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    // --- Admin ---

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]                 // meno nezáleží na veľkosti písmen
    [InlineData("admin@vybavim.local")]   // dá sa prihlásiť aj e-mailom
    public async Task Login_Admin_ReturnsTokens(string login)
    {
        var tokens = await LoginAsync(login, AdminPassword);

        Assert.Equal("Bearer", tokens.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrEmpty(AccessToken(tokens)));
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Me_AsAdmin_HasAdminRole()
    {
        var tokens = await LoginAsync(AdminLogin, AdminPassword);

        var response = await GetAsync("/api/v1/auth/me", AccessToken(tokens));
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Admin", me.GetProperty("userName").GetString());
        Assert.Contains("Admin", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Users_AsAdmin_ReturnsListWithAdmin()
    {
        var tokens = await LoginAsync(AdminLogin, AdminPassword);

        var response = await GetAsync("/api/v1/users", AccessToken(tokens));
        var users = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Admin", users.EnumerateArray().Select(u => u.GetProperty("userName").GetString()));
    }

    // --- Registrácia a bežný používateľ ---

    [Fact]
    public async Task Register_ThenLogin_ThenMe()
    {
        var email = NewEmail();

        var register = await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var tokens = await LoginAsync(email, ValidPassword);
        var me = await (await GetAsync("/api/v1/auth/me", AccessToken(tokens))).Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Empty(me.GetProperty("roles").EnumerateArray());   // bežný používateľ nie je admin
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400InSlovak()
    {
        var response = await PostAsync("/api/v1/auth/register", new { email = NewEmail(), password = "kratke" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("identity.registration_failed", problem.GetProperty("code").GetString());
        Assert.Contains("aspoň 8 znakov", problem.GetProperty("title").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var email = NewEmail();
        await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });

        var second = await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Users_AsNormalUser_Returns403()
    {
        var email = NewEmail();
        await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });
        var tokens = await LoginAsync(email, ValidPassword);

        var response = await GetAsync("/api/v1/users", AccessToken(tokens));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);   // prihlásený, ale nie admin
    }

    // --- Chyby prihlásenia ---

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithCode()
    {
        var email = NewEmail();
        await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });

        var response = await PostAsync("/api/v1/auth/login", new { login = email, password = "Zle12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.invalid_credentials", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount()
    {
        var email = NewEmail();
        await PostAsync("/api/v1/auth/register", new { email, password = ValidPassword });

        for (var i = 0; i < 5; i++)
        {
            await PostAsync("/api/v1/auth/login", new { login = email, password = "Zle12345" });
        }

        // Aj so SPRÁVNYM heslom je účet teraz dočasne zablokovaný
        var response = await PostAsync("/api/v1/auth/login", new { login = email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.locked_out", await ErrorCodeAsync(response));
    }

    // --- Obnovenie tokenu ---

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewAccessToken()
    {
        var tokens = await LoginAsync(AdminLogin, AdminPassword);

        var response = await PostAsync("/api/v1/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() });
        var refreshed = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await GetAsync("/api/v1/auth/me", AccessToken(refreshed));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Returns401()
    {
        var response = await PostAsync("/api/v1/auth/refresh", new { refreshToken = "nezmysel" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("identity.invalid_refresh_token", await ErrorCodeAsync(response));
    }
}
