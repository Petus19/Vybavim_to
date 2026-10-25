using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vybavim.Modules.Profiles.Domain;

namespace Vybavim.Modules.Profiles.Infrastructure.Configurations;

/// <summary>Ako sa trieda Profile uloží do tabuľky profiles.profiles.</summary>
internal sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("profiles");

        // Kľúčom je ID používateľa – jeden používateľ môže mať najviac jeden profil.
        // Zámerne bez cudzieho kľúča na identity.users: iná schéma, iný modul.
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();

        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.DisplayName).HasMaxLength(Profile.DisplayNameMaxLength);
        builder.Property(p => p.City).HasMaxLength(Profile.CityMaxLength);
        builder.Property(p => p.Bio).HasMaxLength(Profile.BioMaxLength);
        builder.Property(p => p.Website).HasMaxLength(Profile.WebsiteMaxLength);
        builder.Property(p => p.Skills).HasColumnType("text[]");
    }
}
