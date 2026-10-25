using Vybavim.Common.Errors;
using Vybavim.Modules.Profiles.Contracts;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Tests.Domain;

/// <summary>Pravidlá profilu: Profile.Create() a Update().</summary>
public sealed class ProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static Profile CreateProfile(
        string displayName = "Jana Kováčová",
        string? city = "Košice",
        string? bio = null,
        IEnumerable<string>? skills = null,
        string? website = null) =>
        Profile.Create(Guid.NewGuid(), ProfileType.Person, displayName, city, bio, skills, website, Now);

    private static void AssertBroken(string expectedCode, Action act)
    {
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(expectedCode, exception.Error.Code);
    }

    [Fact]
    public void Create_ValidInput_TrimsAndKeepsValues()
    {
        var profile = CreateProfile(displayName: "  Jana Kováčová  ", city: "  ", bio: "Grafička", skills: [" Branding ", "branding", ""]);

        Assert.Equal("Jana Kováčová", profile.DisplayName);
        Assert.Null(profile.City);                       // prázdny text = null
        Assert.Equal("Grafička", profile.Bio);
        Assert.Equal(["Branding"], profile.Skills);      // orezané, bez duplikátov a prázdnych
        Assert.Equal(Now, profile.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyDisplayName_Throws(string displayName)
    {
        AssertBroken("profile.display_name_required", () => CreateProfile(displayName: displayName));
    }

    [Fact]
    public void Create_DisplayNameTooLong_Throws()
    {
        AssertBroken("profile.display_name_too_long",
            () => CreateProfile(displayName: new string('a', Profile.DisplayNameMaxLength + 1)));
    }

    [Theory]
    [InlineData("https://jana-grafika.sk")]
    [InlineData("http://portfolio.example.com/jana")]
    public void Create_ValidWebsite_Succeeds(string website)
    {
        Assert.Equal(website, CreateProfile(website: website).Website);
    }

    [Theory]
    [InlineData("jana-grafika.sk")]            // chýba http(s)://
    [InlineData("javascript:alert(1)")]        // nebezpečný odkaz
    [InlineData("ftp://server.sk/subor")]      // iný protokol
    public void Create_InvalidWebsite_Throws(string website)
    {
        AssertBroken("profile.invalid_website", () => CreateProfile(website: website));
    }

    [Fact]
    public void Create_TooManySkills_Throws()
    {
        var skills = Enumerable.Range(1, Profile.SkillsMaxItems + 1).Select(i => $"Zručnosť {i}");

        AssertBroken("profile.too_many_skills", () => CreateProfile(skills: skills));
    }

    [Fact]
    public void Update_ChangesValuesAndUpdatedAt()
    {
        var profile = CreateProfile();
        var later = Now.AddDays(1);

        profile.Update(ProfileType.Company, "Grafika Kováčová s.r.o.", "Bratislava", "Štúdio", ["Logo"], "https://grafika.sk", later);

        Assert.Equal(ProfileType.Company, profile.Type);
        Assert.Equal("Grafika Kováčová s.r.o.", profile.DisplayName);
        Assert.Equal(later, profile.UpdatedAt);
        Assert.Equal(Now, profile.CreatedAt);   // čas vytvorenia sa nemení
    }

    [Fact]
    public void Update_BreakingRule_KeepsOriginalValues()
    {
        var profile = CreateProfile(displayName: "Pôvodné meno");

        AssertBroken("profile.invalid_website",
            () => profile.Update(ProfileType.Company, "Nové meno", null, null, null, "zly-web", Now.AddDays(1)));

        Assert.Equal("Pôvodné meno", profile.DisplayName);
        Assert.Equal(ProfileType.Person, profile.Type);
    }
}
