using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
namespace ContentOS.Infrastructure.Articles;

/// <summary>Bounded public HTTP page reads: no auth/cookies/proxy, only public addresses, validated redirects and default TLS verification.</summary>
public sealed class ArticlePublicPageReader: IArticlePublicPageReader
{
    public static bool IsPublicAddress(IPAddress address)
    {
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        if(IPAddress.IsLoopback(address))return false;
        var b=address.GetAddressBytes();
        if(address.AddressFamily==AddressFamily.InterNetwork)
            return !(b[0] is 0 or 10 or 127||b[0]>=224||b[0]==169&&b[1]==254||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==192&&b[1]==168||b[0]==100&&b[1]>=64&&b[1]<=127||b[0]==192&&b[1]==0||b[0]==198&&b[1] is 18 or 19||b[0]==198&&b[1]==51&&b[2]==100||b[0]==203&&b[1]==0&&b[2]==113);
        // Only global-unicast IPv6; reject private, local, multicast, mapped and unspecified ranges.
        return address.AddressFamily==AddressFamily.InterNetworkV6&&(b[0]&0xe0)==0x20&&!(b[0]==0x20&&b[1]==0x02)&&!(b[0]==0x20&&b[1]==0x01&&((b[2]==0x0d&&b[3]==0xb8)||(b[2]==0&&b[3]==0)));
    }
    public static Uri ValidateUrl(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo)||uri.Port is not (80 or 443)||uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".internal",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Source/link URL is not a supported public HTTP URL.");
        if(IPAddress.TryParse(uri.Host,out var literal)&&!IsPublicAddress(literal))throw new InvalidOperationException("Private source/link destinations are blocked.");return uri;
    }
    public async Task<CollectedArticlePage> ReadAsync(string url,CancellationToken cancellationToken)
    {
        var current=ValidateUrl(url);using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        for(var redirects=0;redirects<=3;redirects++)
        {
            var addresses=await Dns.GetHostAddressesAsync(current.DnsSafeHost,deadline.Token);
            if(addresses.Length==0||addresses.Any(a=>!IsPublicAddress(a)))throw new InvalidOperationException("Source/link DNS resolved to an unsupported private or nonpublic destination.");
            var address=addresses[0];
            using var handler=new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false,ConnectCallback=async(connection,token)=>
            {
                // Pin the already validated address to prevent a second DNS resolution/rebinding.
                var socket=new Socket(address.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
                try{await socket.ConnectAsync(new IPEndPoint(address,current.Port),token);return new NetworkStream(socket,ownsSocket:true);}catch{socket.Dispose();throw;}
            }};
            using var client=new HttpClient(handler);using var request=new HttpRequestMessage(HttpMethod.Get,current);request.Headers.UserAgent.ParseAdd("ContentOS-SourceReview/1.0");
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if((int)response.StatusCode is >=300 and <400&&response.Headers.Location is not null)
            {if(redirects==3)throw new InvalidOperationException("Source/link exceeded the supported redirect bound.");current=ValidateUrl(new Uri(current,response.Headers.Location).AbsoluteUri);continue;}
            var media=response.Content.Headers.ContentType?.MediaType??"";
            if(!response.IsSuccessStatusCode)return new(url,current.AbsoluteUri,(int)response.StatusCode,"",DateTime.UtcNow,"","PublicPinnedHttpReader");
            if(media is not ("text/html" or "text/plain" or "application/xhtml+xml"))throw new InvalidOperationException("Source content type requires an unsupported extractor; no factual excerpt was invented.");
            if(response.Content.Headers.ContentLength>512000)throw new InvalidOperationException("Source page exceeds the supported data bound.");
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var buffer=new MemoryStream();var chunk=new byte[8192];int read;
            while((read=await stream.ReadAsync(chunk,deadline.Token))>0){if(buffer.Length+read>512000)throw new InvalidOperationException("Source page exceeds the supported data bound.");buffer.Write(chunk,0,read);}
            var body=Encoding.UTF8.GetString(buffer.ToArray());
            if(media!="text/plain")
            {body=Regex.Replace(body,@"<(script|style|nav|header|footer)\b[^>]*>[\s\S]*?</\1>"," ",RegexOptions.IgnoreCase);body=Regex.Replace(body,@"<[^>]+>"," ");body=WebUtility.HtmlDecode(body);}
            body=Regex.Replace(body,@"\s+"," ").Trim();
            return new(url,current.AbsoluteUri,(int)response.StatusCode,body,DateTime.UtcNow,FinalArticleDelivery.Hash(Encoding.UTF8.GetBytes(body)),"PublicPinnedHttpReader");
        }
        throw new InvalidOperationException("Source/link could not be collected.");
    }
}
