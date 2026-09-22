using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;

namespace PTBox.Tests;

internal static class IconTests
{
    private static byte[] Png()
    {
        var bitmap=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[] { 0,0,255,255, 0,255,0,255, 255,0,0,255, 255,255,255,255 },8);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }
    public static async Task NativeAsync(string root)
    {
        var original=new TileViewModel(new() { Id="custom-explorer", Path="explorer.exe" },0,root);
        await original.IconReady;
        Check(original.DisplayIcon is BitmapSource { PixelWidth: >=32 },"任意 ID 的 EXE 应从程序提取原图标");
        Check(original.DisplayIcon!.IsFrozen,"跨线程图标必须冻结");
        var custom=Path.Combine(root,"custom-icon.png"); await File.WriteAllBytesAsync(custom,Png());
        var overridden=new TileViewModel(new() { Path="explorer.exe", Icon=custom },0,root); await overridden.IconReady;
        Check(overridden.Icon!=null && ReferenceEquals(overridden.Icon,overridden.DisplayIcon),"自定义图标必须优先");
        var damaged=new TileViewModel(new() { Path="explorer.exe", Icon="missing-icon.png" },0,root); await damaged.IconReady;
        Check(damaged.Icon==null && damaged.DisplayIcon!=null,"自定义图标损坏后应回退程序原图标");
        var missing=new TileViewModel(new() { Id="steam", Path="missing-ptbox-app.exe" },0,root); await missing.IconReady;
        Check(missing.DisplayIcon==null,"不能按 ID 使用绘制的品牌图标冒充程序原图标");
        var unknown=await AppIconService.LoadAsync(new() { Type="uri", Path="ptbox-unregistered-test://open" },root);
        Check(unknown==null,"未注册协议应回退通用图标");
    }
    public static async Task WebsiteAsync(string root)
    {
        var directory=Path.Combine(root,"website-icons"); var png=Png();
        var direct=new Stub(_=>Response(png));
        var icons=new WebsiteIconService(new HttpClient(direct));
        var first=await icons.LoadAsync(new Uri("https://example.invalid/private/page?token=secret"),directory);
        var second=await icons.LoadAsync(new Uri("https://example.invalid/another"),directory);
        Check(first!=null && ReferenceEquals(first,second),"同网站应复用图标");
        Check(direct.Requests.Single().AbsoluteUri=="https://example.invalid/favicon.ico","图标请求不能带用户页面路径或查询参数");
        var offline=new Stub(_=>throw new HttpRequestException("offline"));
        var cached=await new WebsiteIconService(new HttpClient(offline)).LoadAsync(new Uri("https://example.invalid/"),directory);
        Check(cached!=null && offline.Requests.Count==0,"磁盘缓存应跨服务实例且离线可用");
        var linked=new Stub(uri=>uri.AbsolutePath switch
        {
            "/" => new(HttpStatusCode.OK) { Content=new StringContent("<link href='/brand.png' rel='shortcut icon'>") },
            "/brand.png" => Response(png),
            _ => Response("not an image"u8.ToArray())
        });
        var linkedIcon=await new WebsiteIconService(new HttpClient(linked)).LoadAsync(new Uri("https://linked.invalid/"),directory);
        Check(linkedIcon!=null && linked.Requests.Any(x=>x.AbsolutePath=="/brand.png"),"应支持 HTML 声明的网站图标");
        var failure=new WebsiteIconService(new HttpClient(offline));
        Check(await failure.LoadAsync(new Uri("https://offline.invalid/"),directory)==null,"断网失败不能抛异常");
        var oversized=new Stub(uri=>uri.AbsolutePath=="/favicon.ico" ? Response(new byte[2*1024*1024+1]) : new(HttpStatusCode.NotFound));
        Check(await new WebsiteIconService(new HttpClient(oversized)).LoadAsync(new Uri("https://large.invalid/"),directory)==null,"超限图片应拒绝并回退");
    }
    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content=new ByteArrayContent(bytes) };
    private static void Check(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    private sealed class Stub(Func<Uri,HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; }=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        { Requests.Add(request.RequestUri!); return Task.FromResult(respond(request.RequestUri!)); }
    }
}
