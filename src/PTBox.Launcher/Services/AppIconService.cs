using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public static class AppIconService
{
    private static readonly ConcurrentDictionary<string, ImageSource> Icons = new(StringComparer.OrdinalIgnoreCase);
    private static readonly WebsiteIconService Websites = new();
    public static readonly Brush CardBackground = FrozenBrush();
    private static Brush FrozenBrush() { var brush=new SolidColorBrush(Color.FromRgb(26,39,55)); brush.Freeze(); return brush; }

    public static Task<ImageSource?> LoadAsync(LauncherItem item, string dataDirectory)
    {
        if (item.Type == "url" && Uri.TryCreate(item.Path,UriKind.Absolute,out var website) && website.Scheme is "http" or "https")
            return Websites.LoadAsync(website,dataDirectory);
        return Task.Run(() => LoadNative(item,dataDirectory));
    }
    private static ImageSource? LoadNative(LauncherItem item, string dataDirectory)
    {
        try
        {
            if (item.Type == "exe")
            {
                var path = PathService.ResolveExecutable(item.Path,dataDirectory);
                return Path.GetExtension(path).Equals(".lnk",StringComparison.OrdinalIgnoreCase) ? ExtractShortcut(path) : Extract(path);
            }
            if (item.Type != "uri" || !Uri.TryCreate(item.Path,UriKind.Absolute,out var uri)) return null;
            var location = Association(uri.Scheme,15); // ASSOCSTR_DEFAULTICON
            if (!string.IsNullOrWhiteSpace(location))
            {
                var index=0; var comma=location.LastIndexOf(',');
                if (comma >= 0 && int.TryParse(location[(comma+1)..],out index)) location=location[..comma];
                var icon=Extract(Environment.ExpandEnvironmentVariables(location.Trim().Trim('"')),index);
                if (icon != null) return icon;
            }
            var executable=Association(uri.Scheme,2); // ASSOCSTR_EXECUTABLE
            return string.IsNullOrWhiteSpace(executable) ? null : Extract(Environment.ExpandEnvironmentVariables(executable.Trim('"')));
        }
        catch { return null; } // Optional visual metadata must never stop launch/navigation.
    }
    private static string? Association(string scheme, uint kind)
    {
        uint count=32768; var text=new StringBuilder((int)count);
        return AssocQueryString(0x1000,kind,scheme,null,text,ref count)==0 ? text.ToString() : null;
    }
    private static ImageSource? Extract(string path, int index=0)
    {
        if (!File.Exists(path)) return null;
        var key=path+"|"+index+"|"+File.GetLastWriteTimeUtc(path).Ticks;
        if (Icons.TryGetValue(key,out var cached)) return cached;
        IntPtr large=IntPtr.Zero, small=IntPtr.Zero;
        try
        {
            if (SHDefExtractIcon(path,index,0,out large,out small,128 | (16 << 16)) != 0 || large==IntPtr.Zero) return null;
            var image=Imaging.CreateBitmapSourceFromHIcon(large,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); Icons[key]=image; return image;
        }
        finally { if(large!=IntPtr.Zero) DestroyIcon(large); if(small!=IntPtr.Zero) DestroyIcon(small); }
    }
    private static ImageSource? ExtractShortcut(string path)
    {
        var key=path+"|shortcut|"+File.GetLastWriteTimeUtc(path).Ticks;
        if (Icons.TryGetValue(key,out var cached)) return cached;
        // SHGetFileInfo requires COM on the background thread; an existing STA is also usable.
        var com = CoInitializeEx(IntPtr.Zero,0);
        if (com < 0 && com != unchecked((int)0x80010106)) return null; // RPC_E_CHANGED_MODE
        ShellFileInfo info = default;
        try
        {
            if (SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf<ShellFileInfo>(),0x100)==IntPtr.Zero || info.Icon==IntPtr.Zero) return null;
            var image=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            image.Freeze(); Icons[key]=image; return image;
        }
        finally { if(info.Icon!=IntPtr.Zero) DestroyIcon(info.Icon); if(com>=0) CoUninitialize(); }
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string TypeName;
    }
    [DllImport("shell32.dll",EntryPoint="SHGetFileInfoW",CharSet=CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path,uint attributes,out ShellFileInfo info,uint size,uint flags);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll",EntryPoint="SHDefExtractIconW",CharSet=CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string path,int index,uint flags,out IntPtr large,out IntPtr small,uint size);
    [DllImport("shlwapi.dll",EntryPoint="AssocQueryStringW",CharSet=CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags,uint kind,string association,string? extra,StringBuilder output,ref uint count);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}
