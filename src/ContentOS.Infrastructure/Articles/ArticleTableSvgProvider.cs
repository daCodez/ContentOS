using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Workflow;
using ContentOS.Infrastructure.Writing;
using System.Text;
using System.Xml.Linq;
namespace ContentOS.Infrastructure.Articles;

/// <summary>Optional real local SVG table illustrations from exact article cells; no model, paid service, invented values or placeholder fallback.</summary>
public sealed class ArticleTableSvgProvider:IAuthorizedArticleAssetProvider
{
    public async Task<IReadOnlyList<ArticleDeliveryImage>> CreateAsync(GeneratedLongformArticle article,IReadOnlyList<ArticleVisualPlan> visualPlan,string approvedAssetRoot,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(approvedAssetRoot)||!Path.IsPathFullyQualified(approvedAssetRoot)||visualPlan.Count==0)throw new InvalidOperationException("Explicit approved asset root and actual visual plan are required.");
        var root=Path.GetFullPath(approvedAssetRoot);Directory.CreateDirectory(root);var hash=EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article));var output=new List<ArticleDeliveryImage>();
        foreach(var plan in visualPlan)
        {
            var section=article.Sections.SingleOrDefault(s=>s.Heading==plan.SectionHeading)??throw new InvalidOperationException("Visual plan section is missing or ambiguous.");
            var block=section.Paragraphs.FirstOrDefault(p=>p.TrimStart().StartsWith('|')&&p.Contains(plan.ArticleExcerpt,StringComparison.Ordinal))??throw new InvalidOperationException("Local SVG provider supports only planned exact article tables; choose another authorized provider for other visual types.");
            var lines=block.Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
            if(lines.Length<3||!System.Text.RegularExpressions.Regex.IsMatch(lines[1],@"^[|\s:\-]+$"))throw new InvalidOperationException("Planned table is not a readable Markdown table.");
            var rows=lines.Where((_,index)=>index!=1).Select(line=>line.Trim('|').Split('|').Select(c=>c.Trim()).ToArray()).ToArray();
            var columns=rows[0].Length;if(columns is <2 or >4||rows.Length>12||rows.Any(r=>r.Length!=columns||r.Any(c=>c.Length>45||c.Contains('[')||c.Contains('<'))))throw new InvalidOperationException("Table exceeds supported diagram layout or contains unsupported markup; no truncated visual is delivered.");
            XNamespace ns="http://www.w3.org/2000/svg";var width=columns*240+40;var height=rows.Length*56+100;
            var svg=new XElement(ns+"svg",new XAttribute("width",width),new XAttribute("height",height),new XAttribute("viewBox",$"0 0 {width} {height}"),
                new XElement(ns+"title",plan.AltText),new XElement(ns+"rect",new XAttribute("width",width),new XAttribute("height",height),new XAttribute("fill","#f6f8f5")),
                new XElement(ns+"text",new XAttribute("x",20),new XAttribute("y",36),new XAttribute("font-size",22),new XAttribute("font-family","sans-serif"),plan.SectionHeading));
            for(var row=0;row<rows.Length;row++)for(var col=0;col<columns;col++)
            {svg.Add(new XElement(ns+"rect",new XAttribute("x",20+col*240),new XAttribute("y",60+row*56),new XAttribute("width",240),new XAttribute("height",56),new XAttribute("fill",row==0?"#dbece2":"white"),new XAttribute("stroke","#a5b9ad")));svg.Add(new XElement(ns+"text",new XAttribute("x",30+col*240),new XAttribute("y",94+row*56),new XAttribute("font-size",16),new XAttribute("font-family","sans-serif"),rows[row][col]));}
            var bytes=Encoding.UTF8.GetBytes(svg.ToString(SaveOptions.DisableFormatting));var path=Path.Combine(root,$"article-table-{hash[..12]}-{output.Count+1}.svg");
            await File.WriteAllBytesAsync(path,bytes,cancellationToken);
            output.Add(new(path,FinalArticleDelivery.Hash(bytes),plan.AltText,"LocalExactArticleTableSvg",hash));
        }
        return output;
    }
}
