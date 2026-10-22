using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Vybavim.Common.Database;

/// <summary>
/// Niečo, čo vie aktualizovať databázu jedného modulu na najnovšiu verziu (spustiť migrácie).
/// Api nepozná DbContexty modulov (sú internal), pozná iba toto rozhranie.
/// </summary>
public interface IDatabaseMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Migrátor pre konkrétny DbContext. Pre každý modul sa zaregistruje jeden
/// automaticky v <see cref="Extensions.DatabaseExtensions.AddModuleDbContext{TContext}"/>.
/// </summary>
internal sealed partial class DatabaseMigrator<TContext>(TContext context, ILogger<DatabaseMigrator<TContext>> logger)
    : IDatabaseMigrator
    where TContext : DbContext, IModuleDbContext
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            LogUpToDate(logger, context.Schema);
            return;
        }

        LogMigrating(logger, context.Schema, pending.Count, pending);
        await context.Database.MigrateAsync(cancellationToken);
    }

    // [LoggerMessage]: kompilátor z týchto riadkov vygeneruje rýchly kód na logovanie.
    [LoggerMessage(Level = LogLevel.Information, Message = "Schéma {Schema}: databáza je aktuálna")]
    private static partial void LogUpToDate(ILogger logger, string schema);

    [LoggerMessage(Level = LogLevel.Information, Message = "Schéma {Schema}: spúšťam {Count} migrácií: {Migrations}")]
    private static partial void LogMigrating(ILogger logger, string schema, int count, IReadOnlyList<string> migrations);
}
