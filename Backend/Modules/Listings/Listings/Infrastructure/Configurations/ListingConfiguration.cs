using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Infrastructure.Configurations;

/// <summary>Ako sa trieda Listing uloží do tabuľky listings.listings.</summary>
internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    /// <summary>Max. dĺžka enumu uloženého ako text ("Intermediate", "Negotiable"…).</summary>
    private const int EnumMaxLength = 20;

    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();   // Guid v7 vytvára Listing.Create()

        // Texty
        builder.Property(l => l.Title).HasMaxLength(Listing.TitleMaxLength);
        builder.Property(l => l.Description).HasMaxLength(Listing.DescriptionMaxLength);
        builder.Property(l => l.City).HasMaxLength(Listing.CityMaxLength);
        builder.Property(l => l.EstimatedDuration).HasMaxLength(Listing.EstimatedDurationMaxLength);
        builder.Property(l => l.Currency).HasMaxLength(3).IsFixedLength();

        // Enumy ukladáme ako text ("Open", nie 0). V DBeaveri je to čitateľné
        // a pridanie novej hodnoty doprostred enumu nepokazí staré záznamy.
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(l => l.BudgetType).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(l => l.Level).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(l => l.Size).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(l => l.Engagement).HasConversion<string>().HasMaxLength(EnumMaxLength);

        // Zoznamy textov: PostgreSQL ich uloží do jedného stĺpca typu text[] (pole),
        // netreba na ne samostatnú tabuľku.
        builder.Property(l => l.Skills).HasColumnType("text[]");
        builder.Property(l => l.Deliverables).HasColumnType("text[]");
        builder.Property(l => l.Requirements).HasColumnType("text[]");

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
