using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public sealed record LaunchResult(Process? Process, bool WaitForExit, bool Reused);
public sealed class AppLaunchService(string dataDirectory, LoggingService log)
{
    public async Task<LaunchResult> LaunchAsync(LauncherItem item)
    {
        log.Info($"启动应用 {item.Id} ({item.Type}, {item.LaunchBehavior})");
        if (item.Type != "exe")
        {
            var address = ValidateUri(item);
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
            return new(null, false, false);
        }
        var path = PathService.ResolveExecutable(item.Path, dataDirectory);
        if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("EXE 入口只能指向 .exe 程序");
        var existing = item.ReuseExisting ? await Task.Run(() => FindExisting(path)) : null;
        if (existing != null)
        {
            if (!WindowService.BringProcessForward(existing))
            {
                existing.Dispose();
                throw new InvalidOperationException("应用已经运行，但暂时没有可切换的窗口。请稍后重试或从 Windows 桌面打开。");
            }
            return new(existing, item.LaunchBehavior == "waitForExit", true);
        }
        var process = Process.Start(new ProcessStartInfo(path, item.Arguments)
        {
            UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)!
        }) ?? throw new InvalidOperationException("Windows 未返回启动进程");
        return new(process, item.LaunchBehavior == "waitForExit", false);
    }
    public static string ValidateUri(LauncherItem item)
    {
        // Pasted text can include whitespace or invisible direction/BOM markers at its edges.
        // Preserve the address body (including encoded paths, queries and fragments).
        var address = item.Path ?? "";
        var start = 0; var end = address.Length;
        static bool IsPadding(char c) => char.IsWhiteSpace(c) || c is '\u200B' or '\u200E' or '\u200F' or '\uFEFF';
        while (start < end && IsPadding(address[start])) start++;
        while (end > start && IsPadding(address[end - 1])) end--;
        address = address[start..end];
        // Only normalize the HTTP(S) prefix, never characters in a path/query/fragment.
        if (item.Type == "url") address = Regex.Replace(address, @"\A(https?)[:：][/／]{2}", "$1://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var label = $"“{item.Name}”：";
        if (address.Length == 0) throw new InvalidDataException(label + (item.Type == "url" ? "请填写网页地址，例如 https://www.baidu.com。" : "请填写应用协议地址，例如 steam://open/bigpicture。"));
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) throw new InvalidDataException(label + (item.Type == "url" ? "网址无效，请使用 https:// 开头的完整地址，并检查全角符号。" : "应用协议地址无效，请检查协议名称和冒号。"));
        if (item.Type == "url" && uri.Scheme is not ("http" or "https")) throw new InvalidDataException(label + "网页入口仅支持 HTTP / HTTPS。");
        if (uri.Scheme is "file" or "javascript" or "data" or "vbscript" or "shell")
            throw new InvalidDataException(label + "不支持此协议，请使用 EXE 入口或应用 URI。");
        return address;
    }
    private static Process? FindExisting(string path)
    {
        Process? match = null;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
        {
            try
            {
                if (match == null && !process.HasExited && string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase))
                { match = process; continue; }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            process.Dispose();
        }
        return match;
    }
}
