using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PTBox.Launcher.Services;

public sealed class WebsiteIconService
{
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, Lazy<Task<ImageSource?>>> _requests = new();
    private readonly SemaphoreSlim _slots = new(4);
    public WebsiteIconService(HttpClient? http=null)
    {
        _http=http ?? new HttpClient(new HttpClientHandler { UseCookies=false, AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(4) };
    }
    public Task<ImageSource?> LoadAsync(Uri website,string dataDirectory)
    {
        if (website.Scheme is not ("http" or "https")) return Task.FromResult<ImageSource?>(null);
        // Only request the origin, never the user's full URL, query or credentials.
        var origin=new Uri(website.GetLeftPart(UriPartial.Authority));
        if (origin.UserInfo.Length > 0) origin=new UriBuilder(origin) { UserName="", Password="" }.Uri;
        var key=Path.GetFullPath(dataDirectory)+"|"+origin.AbsoluteUri;
        return _requests.GetOrAdd(key,_=>new Lazy<Task<ImageSource?>>(()=>LoadCoreAsync(origin,dataDirectory))).Value;
    }
    private async Task<ImageSource?> LoadCoreAsync(Uri origin,string dataDirectory)
    {
        var cache=Path.Combine(dataDirectory,"Cache","Icons",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(origin.AbsoluteUri)))+".png");
        ImageSource? cached=null;
        try { if(File.Exists(cache)) cached=Decode(await File.ReadAllBytesAsync(cache).ConfigureAwait(false)); } catch { }
        if(cached!=null && DateTime.UtcNow-File.GetLastWriteTimeUtc(cache)<TimeSpan.FromDays(7)) return cached;
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            var iconBytes=await FetchAsync(new Uri(origin,"favicon.ico"),2*1024*1024).ConfigureAwait(false);
            var image=Decode(iconBytes);
            if(image==null)
            {
                var html=Encoding.UTF8.GetString(await FetchAsync(origin,256*1024).ConfigureAwait(false) ?? []);
                foreach(var link in IconLinks(html,origin).Take(3))
                {
                    image=Decode(await FetchAsync(link,2*1024*1024).ConfigureAwait(false));
                    if(image!=null) break;
                }
            }
            if(image==null) return cached;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
                var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create((BitmapSource)image));
                using var data=new MemoryStream(); encoder.Save(data);
                await File.WriteAllBytesAsync(cache,data.ToArray()).ConfigureAwait(false);
            }
            catch { } // A read-only cache must not hide an already downloaded icon.
            return image;
        }
        catch { return cached; }
        finally { _slots.Release(); }
    }
    private async Task<byte[]?> FetchAsync(Uri uri,int maxBytes)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(4));
        for(var redirect=0;redirect<4;redirect++)
        {
            if(uri.Scheme is not ("http" or "https") || uri.UserInfo.Length>0) return null;
            using var response=await _http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            if((int)response.StatusCode is >=300 and <400 && response.Headers.Location is Uri location) { uri=new Uri(uri,location); continue; }
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>maxBytes) return null;
            using var source=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var output=new MemoryStream(); var buffer=new byte[8192]; int count;
            while((count=await source.ReadAsync(buffer,timeout.Token).ConfigureAwait(false))>0)
            { if(output.Length+count>maxBytes) return null; output.Write(buffer,0,count); }
            return output.ToArray();
        }
        return null;
    }
    private static IEnumerable<Uri> IconLinks(string html,Uri origin)
    {
        var links=new List<Uri>();
        foreach(Match tag in Regex.Matches(html,@"<link\b[^>]{0,4096}>",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))
        {
            var attributes=Regex.Matches(tag.Value,"(?<name>rel|href)\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>]+))",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
            var rel=""; var href="";
            foreach(Match attribute in attributes)
                if(attribute.Groups["name"].Value.Equals("rel",StringComparison.OrdinalIgnoreCase)) rel=attribute.Groups["value"].Value;
                else href=WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            if(rel.Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(x=>x.Equals("icon",StringComparison.OrdinalIgnoreCase) || x.Equals("apple-touch-icon",StringComparison.OrdinalIgnoreCase)) && Uri.TryCreate(origin,href,out var link) && link.Scheme is "http" or "https") links.Add(link);
        }
        return links.Distinct();
    }
    private static BitmapSource? Decode(byte[]? bytes)
    {
        if(bytes==null || bytes.Length==0) return null;
        try
        {
            using var stream=new MemoryStream(bytes);
            var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth=128; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch { return null; }
    }
}
