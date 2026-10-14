using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Infrastructure.Configurations;

/// <summary>Ako sa trieda Listing uloží do tabuľky listings.listings.</summary>
internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();   // Guid v7 vytvára Listing.Create()

        builder.Property(l => l.Title).HasMaxLength(Listing.TitleMaxLength);
        builder.Property(l => l.Description).HasMaxLength(Listing.DescriptionMaxLength);
        builder.Property(l => l.City).HasMaxLength(Listing.CityMaxLength);
        builder.Property(l => l.Currency).HasMaxLength(3).IsFixedLength();

        // Enum uložíme ako text ("Open"), nie číslo (0). V DBeaveri je to čitateľné
        // a pridanie nového stavu nepokazí staré záznamy.
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

        // Každý inzerát patrí do kategórie. Kategóriu s inzerátmi nejde zmazať.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(l => l.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexy = "obsah knihy" pre rýchle vyhľadávanie.
        builder.HasIndex(l => l.OwnerId);                       // "moje inzeráty"
        builder.HasIndex(l => new { l.Status, l.CreatedAt });   // "najnovšie otvorené inzeráty"
    }
}
