using Vybavim.Common.Errors;
using Vybavim.Modules.Projects.Domain;

namespace Vybavim.Modules.Projects.Tests.Domain;

/// <summary>Pravidlá ponuky: Offer.Create() a zmeny stavu (prijať, odmietnuť, stiahnuť).</summary>
public sealed class OfferTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static Offer CreateOffer(decimal price = 280m, int deliveryDays = 14, string message = "Robím logá 6 rokov.") =>
        Offer.Create(Guid.NewGuid(), Guid.NewGuid(), price, deliveryDays, message, Now);

    private static void AssertBroken(string expectedCode, Action act)
    {
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal(expectedCode, exception.Error.Code);
    }

    [Fact]
    public void Create_ValidInput_IsPendingWithTrimmedMessage()
    {
        var offer = CreateOffer(message: "  Robím logá 6 rokov.  ");

        Assert.Equal(OfferStatus.Pending, offer.Status);
        Assert.Equal("Robím logá 6 rokov.", offer.Message);
        Assert.Equal(280m, offer.Price);
        Assert.Equal(Now, offer.CreatedAt);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("1000000.01")]
    [InlineData("99.999")]   // viac ako centy
    public void Create_InvalidPrice_Throws(string price)
    {
        AssertBroken("offer.invalid_price", () => CreateOffer(price: decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Offer.MaxDeliveryDays + 1)]
    public void Create_InvalidDeliveryDays_Throws(int days)
    {
        AssertBroken("offer.invalid_delivery_days", () => CreateOffer(deliveryDays: days));
    }

    [Fact]
    public void Create_EmptyMessage_Throws()
    {
        AssertBroken("offer.message_required", () => CreateOffer(message: "   "));
    }

    [Fact]
    public void Create_MessageTooLong_Throws()
    {
        AssertBroken("offer.message_too_long", () => CreateOffer(message: new string('a', Offer.MessageMaxLength + 1)));
    }

    [Fact]
    public void Accept_Pending_BecomesAccepted()
    {
        var offer = CreateOffer();
        var later = Now.AddHours(1);

        offer.Accept(later);

        Assert.Equal(OfferStatus.Accepted, offer.Status);
        Assert.Equal(later, offer.UpdatedAt);
    }

    [Fact]
    public void Decided_OfferCannotChangeAgain()
    {
        // Po rozhodnutí sa ponuka už nemení: stiahnutú nejde prijať, prijatú stiahnuť ani odmietnuť
        var withdrawn = CreateOffer();
        withdrawn.Withdraw(Now);
        AssertBroken("offer.invalid_status_change", () => withdrawn.Accept(Now));

        var accepted = CreateOffer();
        accepted.Accept(Now);
        AssertBroken("offer.invalid_status_change", () => accepted.Withdraw(Now));
        AssertBroken("offer.invalid_status_change", () => accepted.Reject(Now));
    }
}
