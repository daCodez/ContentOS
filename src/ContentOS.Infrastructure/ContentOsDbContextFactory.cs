using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContentOS.Infrastructure;

public sealed class ContentOsDbContextFactory : IDesignTimeDbContextFactory<ContentOsDbContext>
{
    public ContentOsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ContentOsDbContext>();
        optionsBuilder.UseSqlite("Data Source=/home/jarvis_bot/.openclaw/workspace/ContentOS/src/ContentOS.Api/contentos.db");
        return new ContentOsDbContext(optionsBuilder.Options);
    }
}
