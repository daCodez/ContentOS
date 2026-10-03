using ContentOS.Infrastructure.Agents;
using ContentOS.Infrastructure.Writing;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ContentOS.Infrastructure.Articles;

public sealed record ArticleDeliveryImage(string LocalPath,string Sha256,string AltText,string Provider,string ArticleVersionHash,bool IsFixture=false);
public sealed record ArticleTocEntry(string Heading,string Anchor);
public sealed record FinalArticlePackage(string Html,string Markdown,string ArticleVersionHash,string HtmlHash,string MarkdownHash,string PackageHash,
    IReadOnlyList<ArticleTocEntry> Toc,IReadOnlyList<ArticleDeliveryImage> Images,IReadOnlyList<string> RequiredSources,bool IsFixture);
public sealed record PostEditDeliveryEvidence(string ArticleVersionHash,string PackageHash,bool SourceClaimMapComplete,bool ArithmeticPassed,bool LinksPassed,string RubricVersion,bool IsFixture=false);

/// <summary>Builds the exact delivery exports from structured prose and verified local image bytes, with completion hard gates.</summary>
public static class FinalArticleDelivery
{
    public static async Task<FinalArticlePackage> BuildAsync(GeneratedLongformArticle article,IReadOnlyList<ArticleDeliveryImage> images,string approvedAssetRoot,
        IReadOnlyList<string> requiredSources,CancellationToken cancellationToken)
    {
        if(article.IsSynthetic||article.Sections.Count==0)throw new InvalidOperationException("Final delivery requires an actual structured article.");
        if(images.Count==0)throw new InvalidOperationException("Missing required article images; image prompts and remote placeholders are not assets.");
        if(requiredSources.Count==0)throw new InvalidOperationException("Missing required source references.");
        var articleHash=EditorialRevisionService.Hash(EditorialRevisionService.ToDraft(article));
        var prose=string.Join("\n\n",article.IntroParagraphs.Concat(article.Sections.SelectMany(s=>s.Paragraphs)).Concat(article.ConclusionParagraphs).Append(article.CallToAction));
        var links=Regex.Matches(prose,@"\[[^\]]+\]\((https?://[^\s\)]+)\)").Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if(requiredSources.Any(source=>!links.Contains(source)))throw new InvalidOperationException("A required source is absent from the exact final article.");
        var paragraphs=article.IntroParagraphs.Concat(article.Sections.SelectMany(s=>s.Paragraphs)).Concat(article.ConclusionParagraphs).ToArray();
        if(paragraphs.Where(p=>p.Length>=60).GroupBy(p=>Regex.Replace(p.Trim(),@"\s+"," "),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))
            throw new InvalidOperationException("Repeated filler remains in the final article.");
        if(Regex.IsMatch(prose,@"a useful longform article|this (?:section|article) should|the article should|this section addresses",RegexOptions.IgnoreCase))
            throw new InvalidOperationException("Meta-writing remains in the final article.");
        var root=Path.GetFullPath(approvedAssetRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var imageHtml=new List<string>();var imageMarkdown=new List<string>();
        foreach(var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path=Path.GetFullPath(image.LocalPath);
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||string.IsNullOrWhiteSpace(image.AltText)
                ||string.IsNullOrWhiteSpace(image.Provider)||image.Provider.Contains("placeholder",StringComparison.OrdinalIgnoreCase)||image.ArticleVersionHash!=articleHash)
                throw new InvalidOperationException("Image is missing, outside the approved asset root, lacks alt/provenance, is a placeholder or belongs to a stale article version.");
            var length=new FileInfo(path).Length;if(length<32||length>5_000_000)throw new InvalidOperationException("Required image file is empty or exceeds the supported data bound.");
            var bytes=await File.ReadAllBytesAsync(path,cancellationToken);
            if(Hash(bytes)!=image.Sha256)throw new InvalidOperationException("Image bytes differ from the saved asset identity.");
            var mime=VerifyImage(bytes,Path.GetExtension(path));
            var data="data:"+mime+";base64,"+Convert.ToBase64String(bytes);
            imageHtml.Add("<figure><img src=\""+data+"\" alt=\""+WebUtility.HtmlEncode(image.AltText)+"\"/><figcaption>"+WebUtility.HtmlEncode(image.AltText)+"</figcaption></figure>");
            imageMarkdown.Add("!["+image.AltText.Replace("[","\\[").Replace("]","\\]")+"]("+data+")");
        }
        var toc=new List<ArticleTocEntry>();var used=new HashSet<string>(StringComparer.Ordinal){"conclusion","next-step"};
        foreach(var section in article.Sections)
        {
            var basis=Regex.Replace(section.Heading.ToLowerInvariant(),@"[^\p{L}\p{Nd}]+","-").Trim('-');if(basis.Length==0)basis="section";
            var anchor=basis;var index=2;while(!used.Add(anchor))anchor=basis+"-"+index++;
            toc.Add(new(section.Heading,anchor));
        }
        if(article.ConclusionParagraphs.Count>0)toc.Add(new("Conclusion","conclusion"));
        if(!string.IsNullOrWhiteSpace(article.CallToAction))toc.Add(new("Next step","next-step"));
        var html=new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/><title>").Append(WebUtility.HtmlEncode(article.Title))
            .Append("</title><style>body{font:18px/1.7 system-ui,sans-serif;color:#20302b;background:#f6f8f5;margin:0}main{max-width:820px;margin:auto;padding:40px 24px}h1,h2,h3{line-height:1.25}a{color:#176a55}img{max-width:100%;height:auto}table{border-collapse:collapse;width:100%;margin:24px 0}th,td{border:1px solid #ccd6ce;padding:10px;text-align:left}figure{margin:32px 0}figcaption{font-size:14px;color:#52645b}section{scroll-margin-top:24px}details{background:#fff;border:1px solid #ccd6ce;border-radius:8px;padding:14px;margin:16px 0}summary{cursor:pointer;font-weight:650}summary:focus-visible{outline:3px solid #176a55;outline-offset:4px}p,a,td,th{overflow-wrap:anywhere}</style></head><body><main><h1>").Append(WebUtility.HtmlEncode(article.Title)).Append("</h1>");
        var markdown=new StringBuilder("# "+article.Title+"\n\n");
        foreach(var intro in article.IntroParagraphs){html.Append(Render(intro));markdown.Append(intro).Append("\n\n");}
        html.Append("<nav aria-label=\"Table of contents\"><h2>Table of contents</h2><ul>");markdown.Append("## Table of contents\n\n");
        foreach(var entry in toc){html.Append("<li><a href=\"#").Append(entry.Anchor).Append("\">").Append(WebUtility.HtmlEncode(entry.Heading)).Append("</a></li>");markdown.Append("- [").Append(entry.Heading).Append("](#").Append(entry.Anchor).Append(")\n");}
        html.Append("</ul></nav>");markdown.Append('\n');
        foreach(var block in imageHtml)html.Append(block);foreach(var block in imageMarkdown)markdown.Append(block).Append("\n\n");
        for(var index=0;index<article.Sections.Count;index++)
        {
            var section=article.Sections[index];var anchor=toc[index].Anchor;
            html.Append("<section id=\"").Append(anchor).Append("\"><h2>").Append(WebUtility.HtmlEncode(section.Heading)).Append("</h2>");
            markdown.Append("<a id=\"").Append(anchor).Append("\"></a>\n\n## ").Append(section.Heading).Append("\n\n");
            var explorer=section.Heading.Equals("Hypothetical evidence explorer",StringComparison.Ordinal)&&section.Paragraphs.Count>=3&&section.Paragraphs[0].Contains("hypothetical",StringComparison.OrdinalIgnoreCase);
            for(var paragraphIndex=0;paragraphIndex<section.Paragraphs.Count;paragraphIndex++)
            {var paragraph=section.Paragraphs[paragraphIndex];if(explorer&&paragraphIndex>0)html.Append("<details><summary>Reveal clue ").Append(paragraphIndex).Append("</summary>").Append(Render(paragraph)).Append("</details>");else html.Append(Render(paragraph));markdown.Append(paragraph).Append("\n\n");}html.Append("</section>");
        }
        if(article.ConclusionParagraphs.Count>0){html.Append("<section id=\"conclusion\"><h2>Conclusion</h2>");markdown.Append("<a id=\"conclusion\"></a>\n\n## Conclusion\n\n");foreach(var p in article.ConclusionParagraphs){html.Append(Render(p));markdown.Append(p).Append("\n\n");}html.Append("</section>");}
        if(!string.IsNullOrWhiteSpace(article.CallToAction)){html.Append("<section id=\"next-step\"><h2>Next step</h2>").Append(Render(article.CallToAction)).Append("</section>");markdown.Append("<a id=\"next-step\"></a>\n\n## Next step\n\n").Append(article.CallToAction);}
        html.Append("</main></body></html>");var htmlText=html.ToString();var markdownText=markdown.ToString();
        var htmlHash=Hash(Encoding.UTF8.GetBytes(htmlText));var markdownHash=Hash(Encoding.UTF8.GetBytes(markdownText));
        return new(htmlText,markdownText,articleHash,htmlHash,markdownHash,Hash(Encoding.UTF8.GetBytes(articleHash+htmlHash+markdownHash+string.Join("|",images.Select(i=>i.Sha256)))),toc,images.ToArray(),requiredSources.ToArray(),images.Any(i=>i.IsFixture));
    }
    public static bool CanComplete(FinalArticlePackage package,decimal score,string scoredPackageHash,string? approvedBy,string? approvedPackageHash,PostEditDeliveryEvidence? postEdit=null)
        =>!package.IsFixture&&score>=85&&score<=100&&scoredPackageHash==package.PackageHash&&!string.IsNullOrWhiteSpace(approvedBy)&&approvedPackageHash==package.PackageHash
            &&postEdit is not null&&!postEdit.IsFixture&&postEdit.ArticleVersionHash==package.ArticleVersionHash&&postEdit.PackageHash==package.PackageHash&&postEdit.SourceClaimMapComplete&&postEdit.ArithmeticPassed&&postEdit.LinksPassed&&!string.IsNullOrWhiteSpace(postEdit.RubricVersion);
    public static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    private static string VerifyImage(byte[] bytes,string extension)
    {
        if(extension.Equals(".svg",StringComparison.OrdinalIgnoreCase))
        {
            try{var xml=XDocument.Parse(Encoding.UTF8.GetString(bytes));if(xml.Root?.Name.LocalName!="svg"||xml.Descendants().Any(e=>e.Name.LocalName is "script" or "foreignObject")||xml.Descendants().Attributes().Any(a=>a.Name.LocalName.StartsWith("on",StringComparison.OrdinalIgnoreCase)||a.Name.LocalName is "href" or "src"))throw new InvalidOperationException("Unsafe or externally dependent SVG.");return "image/svg+xml";}
            catch(System.Xml.XmlException){throw new InvalidOperationException("Required SVG image is invalid.");}
        }
        if(extension.Equals(".png",StringComparison.OrdinalIgnoreCase)&&bytes.Length>=24&&bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))
        { VerifyPng(bytes);return "image/png"; }
        throw new InvalidOperationException("Image format is unsupported or bytes do not match a readable verified image.");
    }
    private static void VerifyPng(byte[] bytes)
    {
        try
        {
            var offset=8;int width=0,height=0,channels=0;var header=false;var ended=false;var sawData=false;var dataEnded=false;
            using var compressed=new MemoryStream();
            while(offset<bytes.Length)
            {
                if(bytes.Length-offset<12)throw new InvalidOperationException("Truncated PNG chunk.");
                var length=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));
                if(length>5_000_000||length>bytes.Length-offset-12)throw new InvalidOperationException("Invalid PNG chunk length.");
                var count=(int)length;var type=Encoding.ASCII.GetString(bytes,offset+4,4);var payload=bytes.AsSpan(offset+8,count);
                uint crc=0xffffffff;
                foreach(var item in bytes.AsSpan(offset+4,count+4))
                {crc^=item;for(var bit=0;bit<8;bit++)crc=(crc&1)!=0?(crc>>1)^0xedb88320:crc>>1;}
                if((crc^0xffffffff)!=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+count,4)))throw new InvalidOperationException("PNG chunk checksum differs.");
                if(!header&&type!="IHDR")throw new InvalidOperationException("PNG must start with IHDR.");
                if(type=="IHDR")
                {
                    if(header||count!=13)throw new InvalidOperationException("Invalid PNG header.");
                    var w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(payload[..4]);var h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4,4));
                    if(w<=1||h<=1||w>4096||h>4096||payload[8]!=8||payload[10]!=0||payload[11]!=0||payload[12]!=0)throw new InvalidOperationException("Unsupported PNG dimensions, depth or interlacing.");
                    width=(int)w;height=(int)h;channels=payload[9] switch{0=>1,2=>3,4=>2,6=>4,_=>throw new InvalidOperationException("Unsupported PNG color type.")};header=true;
                }
                else if(type=="IDAT")
                {if(dataEnded)throw new InvalidOperationException("PNG data chunks must be consecutive.");sawData=true;compressed.Write(payload);}
                else if(type=="IEND")
                {if(count!=0||!sawData||offset+12!=bytes.Length)throw new InvalidOperationException("Invalid PNG end chunk.");ended=true;break;}
                else
                {if(sawData)dataEnded=true;if(type.Length!=4||char.IsUpper(type[0]))throw new InvalidOperationException("Unsupported critical PNG chunk.");}
                offset+=count+12;
            }
            if(!ended)throw new InvalidOperationException("PNG image is incomplete.");
            compressed.Position=0;using var inflater=new System.IO.Compression.ZLibStream(compressed,System.IO.Compression.CompressionMode.Decompress);
            var row=new byte[checked(width*channels+1)];
            for(var y=0;y<height;y++)
            {inflater.ReadExactly(row);if(row[0]>4)throw new InvalidOperationException("Invalid PNG scanline filter.");}
            if(inflater.ReadByte()!=-1)throw new InvalidOperationException("PNG pixel data exceeds its dimensions.");
        }
        catch(Exception ex) when(ex is InvalidDataException or EndOfStreamException or OverflowException)
        {throw new InvalidOperationException("PNG image data cannot be decoded.",ex);}
    }
    private static string Inline(string text)=>Regex.Replace(WebUtility.HtmlEncode(text),@"\[([^\]]+)\]\((https?://[^\s\)]+)\)",m=>"<a href=\""+m.Groups[2].Value+"\">"+m.Groups[1].Value+"</a>");
    private static string Render(string block)
    {
        var lines=block.Split('\n').Select(l=>l.Trim()).Where(l=>l.Length>0).ToArray();
        if(lines.Length>=2&&lines[0].StartsWith('|')&&Regex.IsMatch(lines[1],@"^[|\s:\-]+$"))
        {
            string Cells(string line,string tag)=>string.Join("",line.Trim('|').Split('|').Select(cell=>"<"+tag+">"+Inline(cell.Trim())+"</"+tag+">"));
            return "<table><thead><tr>"+Cells(lines[0],"th")+"</tr></thead><tbody>"+string.Join("",lines.Skip(2).Select(line=>"<tr>"+Cells(line,"td")+"</tr>"))+"</tbody></table>";
        }
        if(lines.All(l=>Regex.IsMatch(l,@"^[-*+] ")))
            return "<ul>"+string.Join("",lines.Select(line=>"<li>"+(Regex.IsMatch(line,@"^[-*+] \[[ xX]\] ")?"<input type=\"checkbox\" disabled"+(line[3] is 'x' or 'X'?" checked":"")+"/> "+Inline(line[6..]):Inline(line[2..]))+"</li>"))+"</ul>";
        if(lines.All(l=>Regex.IsMatch(l,@"^\d+\. ")))
            return "<ol>"+string.Join("",lines.Select(line=>"<li>"+Inline(Regex.Replace(line,@"^\d+\. ",""))+"</li>"))+"</ol>";
        if(lines[0].StartsWith("### "))return "<h3>"+Inline(lines[0][4..])+"</h3>"+string.Join("",lines.Skip(1).Select(line=>"<p>"+Inline(line)+"</p>"));
        return "<p>"+Inline(block).Replace("\n","<br/>")+"</p>";
    }
}
