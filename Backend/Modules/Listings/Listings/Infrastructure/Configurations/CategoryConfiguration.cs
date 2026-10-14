using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Listings.Domain;

namespace Vybavim.Modules.Listings.Infrastructure.Configurations;

/// <summary>Ako sa trieda Category uloží do tabuľky listings.categories.</summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();   // ID generujeme my, nie databáza

        builder.Property(c => c.Slug).HasMaxLength(Category.SlugMaxLength);
        builder.HasIndex(c => c.Slug).IsUnique();             // dve kategórie nesmú mať rovnaký slug

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength);

        // Strom kategórií: ParentId ukazuje na inú kategóriu v tej istej tabuľke.
        // Restrict = nadradenú kategóriu nejde zmazať, kým má podkategórie.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
