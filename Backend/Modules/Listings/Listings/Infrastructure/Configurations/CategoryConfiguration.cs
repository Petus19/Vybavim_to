using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Listings.Domain;
using Vybavim.Modules.Listings.Infrastructure.Seeding;

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
        builder.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);

        // Strom kategórií: ParentId ukazuje na inú kategóriu v tej istej tabuľke.
        // Restrict = nadradenú kategóriu nejde zmazať, kým má podkategórie.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Referenčné dáta: tieto kategórie musia existovať všade (vývoj aj produkcia),
        // preto idú priamo do migrácie. ID sú pevné, aby boli rovnaké v každej databáze.
        // Slugy sa zhodujú s webom (Frontend/Web/src/lib/listings.ts).
        builder.HasData(
            new Category(CategoryIds.Web, "web", "Web a aplikácie", "Weby, e-shopy, mobilné appky", null, 1),
            new Category(CategoryIds.Graphics, "grafika", "Grafika a logo", "Logá, vizuály, tlačoviny", null, 2),
            new Category(CategoryIds.ItSupport, "it-podpora", "IT podpora", "Siete, počítače, servery", null, 3),
            new Category(CategoryIds.Marketing, "marketing", "Online marketing", "Reklamy, sociálne siete, SEO", null, 4));
    }
}
