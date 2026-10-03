using ContentOS.Application.Research;
using ContentOS.Application.Abstractions;
using ContentOS.Domain.Entities;
using ContentOS.Infrastructure.Research;
using ContentOS.Infrastructure.Ideation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;
public class ResearchTargetRegressionTests
{
    [TestCase(20,1)]
    [TestCase(30,1)]
    [TestCase(30,0)]
    public async Task EvidenceLimitedRunReturnsFewerIdeasWithAnExplicitReason(int requested,int available)
    {
        await using var db=new ContentOsDbContext(new DbContextOptionsBuilder<ContentOsDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var site=new Site { Id=Guid.NewGuid(),Name="Offline research fixture",Domain="https://example.org",Niche="Variable income",IsActive=true };
        db.Sites.Add(site);await db.SaveChangesAsync();
        var source=new ResearchFinding { ProviderName="Fixture",SourceType="FixtureEvidence",SourceTitle="Cash flow fixture",SourceUrl="https://example.org/collected",SourceExcerpt="In this offline fixture, variable pay arrives after essential bill due dates and requires planning available cash.",PainPoint="Variable pay",KeywordSuggestion="irregular income budget" };
        var provider=Substitute.For<IResearchSourceProvider>();provider.Name.Returns("Fixture");
        provider.ResearchAsync(Arg.Any<ResearchContext>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<ResearchFinding>>([source]));
        var ideation=Substitute.For<IIdeationAgent>();
        IList<CandidateContentIdea> candidates=available==0?[]:[new CandidateContentIdea { Title="Match changing pay to bill due dates",PrimaryKeyword="irregular income budget",AudiencePainPoint="Variable pay",AudienceGoal="Plan essentials",RecommendedAngle="practical",SearchIntent="informational",SupportingFindings=[source] }];
        ideation.GenerateIdeasAsync(Arg.Any<ResearchContext>(),Arg.Any<IList<ResearchFinding>>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult(candidates));
        var agent=new ResearchAgent(db,[provider],ideation,new IdeaDeduplicatorService(),NullLogger<ResearchAgent>.Instance);
        var result=await agent.RunAsync(site.Id,requested);
        Assert.That(result.RequestedIdeaCount,Is.EqualTo(requested));
        Assert.That(result.IdeasSaved,Is.EqualTo(available));
        Assert.That(await db.ContentIdeas.CountAsync(),Is.EqualTo(available));
        Assert.That(result.ShortfallReason,Does.Contain($"Saved {available} of {requested}"));
    }
}
