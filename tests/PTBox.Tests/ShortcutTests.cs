using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;

namespace PTBox.Tests;

internal static class ShortcutTests
{
    internal static void CreateShortcut(string path, string target, string arguments = "", string workingDirectory = "")
    {
        // Produce a real Windows shortcut independently of the application's import/launch code.
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        object? shortcut = null;
        try
        {
            dynamic link = shell.CreateShortcut(path); shortcut = link;
            link.TargetPath = target; link.Arguments = arguments; link.WorkingDirectory = workingDirectory;
            link.IconLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") + ",0";
            link.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public static async Task Run(string fixture, string root)
    {
        var folder = Path.Combine(root, "快捷方式 测试"); Directory.CreateDirectory(folder);
        var working = Path.Combine(folder, "工作目录"); Directory.CreateDirectory(working);
        var context = Path.Combine(folder, "启动结果.json");
        var close = Path.Combine(folder, "close");
        var link = Path.Combine(folder, "我的程序.LNK");
        CreateShortcut(link, Path.GetFullPath(fixture), $"--lifetime 4000 --context-file \"{context}\" --close-file \"{close}\" --sample \"中文 参数\"", working);
        var item = AppLaunchService.FromLocalFile(link);
        Check(item.Name == "我的程序" && item.Path == link && item.Type == "exe" && !item.ReuseExisting && item.LaunchBehavior == "fireAndForget", "保留快捷方式路径，并使用适合 Shell 的默认启动行为");
        var config = new ConfigService(folder, new(folder)); config.Save(new LauncherConfig { Apps = [item] });
        item = config.Load().Apps.Single();
        var icon = new TileViewModel(item, 0, folder); await icon.IconReady;
        Check(icon.DisplayIcon != null, "读取真实快捷方式图标");
        Check(!File.Exists(context), "导入和读取图标不能启动目标程序");
        var launched = await new AppLaunchService(folder, new(folder)).LaunchAsync(item);
        using var process = launched.Process;
        try
        {
            var watch = Stopwatch.StartNew();
            while (!File.Exists(context) && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(50);
            Check(File.Exists(context), "实际启动快捷方式目标");
            // The writer may just have created the file; wait for the small JSON write to complete.
            JsonDocument? data = null;
            while (data == null && watch.Elapsed < TimeSpan.FromSeconds(10))
            {
                try { data = JsonDocument.Parse(File.ReadAllText(context)); }
                catch (Exception ex) when (ex is IOException or JsonException) { await Task.Delay(50); }
            }
            using var document = data ?? throw new InvalidOperationException("未读取到启动结果");
            Check(document.RootElement.GetProperty("Directory").GetString() == working, "保留快捷方式工作目录");
            Check(document.RootElement.GetProperty("Arguments").EnumerateArray().Any(x => x.GetString() == "中文 参数"), "保留快捷方式引号与中文参数");
            Check(!launched.WaitForExit, "快捷方式默认不依赖不可靠的 Shell 进程等待");
        }
        finally
        {
            File.WriteAllText(close, "close");
            if (process != null) await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }

        var internet = Path.Combine(folder, "我的游戏.URL");
        File.WriteAllText(internet, "[InternetShortcut]\r\nURL=steam://rungameid/123\r\n", Encoding.Unicode);
        var game = AppLaunchService.FromLocalFile(internet);
        Check(game.Type == "uri" && game.Path == "steam://rungameid/123" && game.Name == "我的游戏" && !game.ReuseExisting, "导入游戏协议快捷方式");
        File.WriteAllText(internet, "[Ignored]\nURL=https://wrong.invalid\n[InternetShortcut]\nURL=https://example.com/?a=1&b=2\n");
        var website = AppLaunchService.FromLocalFile(internet);
        Check(website.Type == "url" && website.Path == "https://example.com/?a=1&b=2", "仅读取正确段落并保留 URL 参数");
        foreach (var contents in new[] { "[InternetShortcut]\n", "[InternetShortcut]\nURL=javascript:alert(1)", "[InternetShortcut]\nURL=file:///C:/Windows/explorer.exe" })
        {
            File.WriteAllText(internet, contents);
            try { AppLaunchService.FromLocalFile(internet); throw new InvalidOperationException("应拒绝损坏或不支持的地址"); }
            catch (InvalidDataException) { }
        }
        File.Delete(link);
        try { await new AppLaunchService(folder, new(folder)).LaunchAsync(item); throw new InvalidOperationException("应提示快捷方式已丢失"); }
        catch (FileNotFoundException) { }
    }

    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
