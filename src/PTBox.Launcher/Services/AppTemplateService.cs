using System.IO;
using Microsoft.Win32;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public sealed record AppTemplate(string Id, string Name, string DetectedPath)
{
    public string Status => DetectedPath.Length > 0 ? "已检测到安装 · 一键添加" : "未检测到安装 · 添加后可设置路径";
}

public static class AppTemplateService
{
    public static IReadOnlyList<AppTemplate> Discover() =>
    [
        new("steam", "Steam", Detect("steam.exe", [RegistryValue(@"Software\Valve\Steam", "SteamExe"), @"%ProgramFiles(x86)%\Steam\steam.exe", @"%ProgramFiles%\Steam\steam.exe"])),
        new("moonlight", "Moonlight", Detect("Moonlight.exe", [@"%ProgramFiles%\Moonlight Game Streaming\Moonlight.exe", @"%ProgramFiles(x86)%\Moonlight Game Streaming\Moonlight.exe", @"%LOCALAPPDATA%\Moonlight Game Streaming\Moonlight.exe"]))
    ];
    private static string RegistryValue(string key, string name)
    {
        try { using var registry = Registry.CurrentUser.OpenSubKey(key); return registry?.GetValue(name) as string ?? ""; }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { return ""; }
    }
    private static string Detect(string executable, string[] candidates)
    {
        foreach (var candidate in candidates.Append(executable))
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try { return PathService.ResolveExecutable(candidate, AppContext.BaseDirectory); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
        }
        return "";
    }
    public static LauncherItem Create(string id, string detectedPath = "") => id switch
    {
        "steam" => new() { Id="steam", Name="Steam", Subtitle="BIG PICTURE", Category="games", Type=detectedPath.Length > 0 ? "exe" : "uri", Path=detectedPath.Length > 0 ? detectedPath : "steam://open/bigpicture", Arguments=detectedPath.Length > 0 ? "-bigpicture" : "", LaunchBehavior="fireAndForget", ReuseExisting=false, Accent="#35465C" },
        "moonlight" => new() { Id="moonlight", Name="Moonlight", Subtitle="游戏与桌面串流", Category="streaming", Path=detectedPath, Accent="#205955" },
        _ => throw new ArgumentException("未知的内置应用模板", nameof(id))
    };
}
