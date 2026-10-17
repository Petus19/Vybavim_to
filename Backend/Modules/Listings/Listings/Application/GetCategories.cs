using Microsoft.EntityFrameworkCore;
using Vybavim.Modules.Listings.Infrastructure;

namespace Vybavim.Modules.Listings.Application;

/// <summary>Prípad použitia: „zobraz hlavné kategórie“.</summary>
internal sealed class GetCategories(ListingsDbContext db)
{
    public Task<List<CategoryResponse>> HandleAsync(CancellationToken cancellationToken) =>
        db.Categories
            .AsNoTracking()                         // iba čítame, EF nemusí sledovať zmeny
            .Where(c => c.ParentId == null)         // iba hlavné kategórie
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryResponse(c.Slug, c.Name, c.Description))
            .ToListAsync(cancellationToken);
}
