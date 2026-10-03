using ContentOS.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class FreshSqliteDatabaseTests
{
    private static ContentOsDbContext CreateDb(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);

    [Test]
    public async Task EmptyDatabaseHasCompleteSchemaAndOnlyFixedBaselineHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await ContentOsDatabaseInitializer.InitializeAsync(db);
        Assert.That(await db.WorkflowTemplates.CountAsync(), Is.Zero);
        Assert.That(await db.ContentIdeas.CountAsync(), Is.Zero);
        Assert.That(await db.Sites.CountAsync(), Is.Zero);
        Assert.That(await db.Database.GetAppliedMigrationsAsync(), Is.EqualTo(new[] { "20260419003557_SplitIdeaArticleWorkflowPipelines" }));
    }

    [Test]
    public async Task ReinitializationPreservessqliteNotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await ContentOsDatabaseInitializer.InitializeAsync(db);
        var id = Guid.NewGuid();
        db.Sites.Add(new Site { Id = id, Name = "Preserve me", Domain = "example.test" });
        await db.SaveChangesAsync();
        await ContentOsDatabaseInitializer.InitializeAsync(db);
        Assert.That((await db.Sites.SingleAsync(s => s.Id == id)).Name, Is.EqualTo("Preserve me"));
    }

    [Test]
    public async Task ExistingDatabaseKeepsLegacyMigrationPathWithoutFreshBaseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDb(connection);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE sqliteNotes (Value TEXT); INSERT INTO sqliteNotes VALUES ('preserved');");
        await ContentOsDatabaseInitializer.InitializeAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM sqliteNotes";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("preserved"));
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='WorkflowTemplates'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero, "Fresh baseline must never be applied to an existing database.");
    }
    [Test]
    public async Task BaselineFailureRollsBackSchemaAndHistoryTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite(connection)
            .AddInterceptors(new FailHistoryInsert()).Options;
        await using var db = new ContentOsDbContext(options);
        Assert.ThrowsAsync<InvalidOperationException>(() => ContentOsDatabaseInitializer.InitializeAsync(db));
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT GLOB 'sqlite_*'";
        Assert.That(Convert.ToInt64(await command.ExecuteScalarAsync()), Is.Zero);
    }

    private sealed class FailHistoryInsert : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("INSERT INTO \"__EFMigrationsHistory\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Simulated history-write failure");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
