using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ContentOS.Infrastructure.Tests;

public class WebRouteRegressionTests
{
    [Test]
    public void WorkflowSidebarTargetsHaveRegisteredRoutes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ContentOS.sln"))) root = root.Parent;
        var components = Path.Combine(root!.FullName, "src", "ContentOS.Web", "Components");
        var nav = File.ReadAllText(Path.Combine(components, "Layout", "NavMenu.razor"));
        var routes = Directory.GetFiles(Path.Combine(components, "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "^@page\\s+\"([^\"]+)\"", RegexOptions.Multiline).Select(m => m.Groups[1].Value))
            .Select(route => "^" + Regex.Replace(Regex.Escape(route), @"\\\{[^}]+}", "[^/]+") + "$").ToArray();
        var workflowLinks = Regex.Matches(nav, "href=\"(/workflow-settings/[^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.That(workflowLinks, Has.Length.EqualTo(2));
        foreach (var link in workflowLinks)
            Assert.That(routes.Any(pattern => Regex.IsMatch(link, pattern)), Is.True, $"Sidebar target {link} must resolve to a registered page");
    }
    [Test]
    public void MainLayoutUsesTheExistingGridShellClass()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ContentOS.sln"))) root = root.Parent;
        var layout = File.ReadAllText(Path.Combine(root!.FullName, "src", "ContentOS.Web", "Components", "Layout", "MainLayout.razor"));
        var shell = Regex.Match(layout, "<div class=\"([^\"]*app-shell[^\"]*)\"").Groups[1].Value.Split(' ');
        Assert.That(shell, Does.Contain("app"), "The stylesheet defines .app as the desktop grid; without it the viewport-height sidebar pushes main content below the screen");
    }
    [Test]
    public void EachWebRouteHasOneComponentOwner()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ContentOS.sln"))) root = root.Parent;
        Assert.That(root, Is.Not.Null, "Checkout root required for Razor route regression");
        var pages = Path.Combine(root!.FullName, "src", "ContentOS.Web", "Components", "Pages");
        var routes = Directory.GetFiles(pages, "*.razor").SelectMany(file =>
            Regex.Matches(File.ReadAllText(file), "^@page\\s+\"([^\"]+)\"", RegexOptions.Multiline)
                .Select(m => new { Route = m.Groups[1].Value, File = Path.GetFileName(file) }));
        var duplicates = routes.GroupBy(r => r.Route, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(r => r.File))}").ToArray();
        Assert.That(duplicates, Is.Empty, "Ambiguous routes crash Blazor's route table on every page");
    }
}
