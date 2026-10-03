using ContentOS.Domain.Entities;
using ContentOS.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WorkflowTemplateSeederTests
{
    private ContentOsDbContext _dbContext = null!;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<ContentOsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _dbContext = new ContentOsDbContext(options);
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();

    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task SeedAsync_CreatesTemplateAndTasks()
    {
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        var template = await _dbContext.WorkflowTemplates
            .FirstOrDefaultAsync(x => x.Name == "LongFormBlogArticle");

        template.Should().NotBeNull();
        template!.Description.Should().NotBeEmpty();
        template.Version.Should().BeGreaterThanOrEqualTo(1);
        template.IsActive.Should().BeTrue();

        var tasks = await _dbContext.WorkflowTaskTemplates
            .Where(x => x.WorkflowTemplateId == template.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        tasks.Should().HaveCount(9);
        tasks[0].Name.Should().Be("Research Topic and Intent");
        tasks[0].StageName.Should().Be("Research");
        tasks[0].AssignedAgent.Should().Be("content-research");
        tasks[8].Name.Should().Be("Humanize Final Draft");
        tasks[8].StageName.Should().Be("Humanizer");
    }

    [Test]
    public async Task SeedAsync_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        var templates = await _dbContext.WorkflowTemplates
            .Where(x => x.Name == "LongFormBlogArticle")
            .ToListAsync();

        templates.Should().HaveCount(1);

        var template = templates[0];
        var tasks = await _dbContext.WorkflowTaskTemplates
            .Where(x => x.WorkflowTemplateId == template.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        // Should still have exactly 9 active tasks
        tasks.Should().HaveCount(9);
    }

    [Test]
    public async Task SeedAsync_UpdatesExistingTemplateOnReRun()
    {
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        // Modify template to simulate a stale state
        var template = await _dbContext.WorkflowTemplates.FirstAsync(x => x.Name == "LongFormBlogArticle");
        var originalDescription = template.Description;
        template.IsActive = false;
        await _dbContext.SaveChangesAsync();

        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        template = await _dbContext.WorkflowTemplates.FirstAsync(x => x.Name == "LongFormBlogArticle");
        template.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task SeedAsync_TaskTemplatesHaveCorrectAgentAssignments()
    {
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        var template = await _dbContext.WorkflowTemplates.FirstAsync(x => x.Name == "LongFormBlogArticle");
        var tasks = await _dbContext.WorkflowTaskTemplates
            .Where(x => x.WorkflowTemplateId == template.Id && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync();

        var expectedAgents = new[]
        {
            "content-research", "keyword-strategy", "seo-optimization-loop",
            "content-planner", "content-writer", "content-seo",
            "content-monetization", "content-scoring", "content-humanizer"
        };

        for (var i = 0; i < expectedAgents.Length; i++)
        {
            tasks[i].AssignedAgent.Should().Be(expectedAgents[i],
                $"Task {tasks[i].Name} should have agent {expectedAgents[i]}");
        }
    }

    [Test]
    public async Task SeedAsync_AllTasksAutoStartWhenPreviousComplete()
    {
        await WorkflowTemplateSeeder.SeedAsync(_dbContext);

        var template = await _dbContext.WorkflowTemplates.FirstAsync(x => x.Name == "LongFormBlogArticle");
        var tasks = await _dbContext.WorkflowTaskTemplates
            .Where(x => x.WorkflowTemplateId == template.Id && x.IsActive)
            .ToListAsync();

        tasks.Should().OnlyContain(t => t.AutoStartWhenPreviousComplete);
    }
}