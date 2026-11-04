using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Projects.Domain;

namespace Vybavim.Modules.Projects.Infrastructure.Configurations;

/// <summary>Ako sa trieda Offer uloží do tabuľky projects.offers.</summary>
internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();   // Guid v7 vytvára Offer.Create()

        builder.Property(o => o.Message).HasMaxLength(Offer.MessageMaxLength);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        // ListingId a FreelancerId bez cudzích kľúčov: inzeráty aj používatelia sú v iných moduloch (schémach).

        // Poistka v databáze: jeden odborník môže mať na inzeráte najviac jednu živú ponuku.
        // Use case to kontroluje aj sám (pekná chyba 409); index zachráni prípad dvoch súčasných kliknutí.
        builder.HasIndex(o => new { o.ListingId, o.FreelancerId })
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Accepted')");

        builder.HasIndex(o => o.FreelancerId);   // „moje ponuky“
    }
}
