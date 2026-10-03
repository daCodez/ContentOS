using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace ContentOS.Infrastructure;

/// <summary>Bootstraps only empty SQLite databases; existing databases retain their migration path.</summary>
public static class ContentOsDatabaseInitializer
{
    private const string BaselineMigration = "20260419003557_SplitIdeaArticleWorkflowPipelines";

    public static async Task InitializeAsync(ContentOsDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            var closeConnection = db.Database.GetDbConnection().State != ConnectionState.Open;
            await db.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT GLOB 'sqlite_*'";
                var tableCount = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
                if (tableCount == 0)
                {
                    // This immutable baseline represents only the named historical migration.
                    // Future migrations are applied below, never recorded as already applied here.
                    using var resource = typeof(ContentOsDatabaseInitializer).Assembly.GetManifestResourceStream(
                        "ContentOS.Infrastructure.Database.FreshSqliteBaseline.sql")
                        ?? throw new InvalidOperationException("The fresh SQLite baseline resource is missing.");
                    using var reader = new StreamReader(resource);
                    var schema = await reader.ReadToEndAsync(cancellationToken);
                    await db.Database.ExecuteSqlRawAsync(schema, cancellationToken);
                    var history = db.GetService<IHistoryRepository>();
                    await db.Database.ExecuteSqlRawAsync(history.GetCreateScript(), cancellationToken);
                    await db.Database.ExecuteSqlRawAsync(
                        history.GetInsertScript(new HistoryRow(BaselineMigration, "10.0.5")), cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                if (closeConnection) await db.Database.CloseConnectionAsync();
            }
        }

        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            await db.Database.MigrateAsync(cancellationToken);
        else
            await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
