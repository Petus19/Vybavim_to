using Microsoft.EntityFrameworkCore;
using Vybavim.Common.Database;
using Vybavim.Modules.Projects.Domain;

namespace Vybavim.Modules.Projects.Infrastructure;

/// <summary>Databáza modulu Projects. Vidí iba tabuľky v schéme "projects".</summary>
internal sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : BaseDbContext(options)
{
    public const string SchemaName = "projects";

    public override string Schema => SchemaName;

    public DbSet<Offer> Offers => Set<Offer>();
}
