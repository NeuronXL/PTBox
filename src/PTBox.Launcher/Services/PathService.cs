using System.IO;
using Microsoft.Win32;

namespace PTBox.Launcher.Services;

public static class PathService
{
    public static string ResolveAsset(string path, string dataDirectory) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path), dataDirectory);

    public static string ResolveExecutable(string path, string dataDirectory)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (string.IsNullOrWhiteSpace(path)) throw new FileNotFoundException("尚未配置程序路径");
        if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains('/'))
        {
            var full = Path.GetFullPath(path, dataDirectory);
            if (File.Exists(full)) return full;
            throw new FileNotFoundException("程序路径不存在", full);
        }
        var name = Path.HasExtension(path) ? path : path + ".exe";
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + name);
            if (key?.GetValue(null) is string registered && File.Exists(registered.Trim('"'))) return registered.Trim('"');
        }
        var dirs = new[] { dataDirectory, Environment.GetFolderPath(Environment.SpecialFolder.System), Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'));
        foreach (var dir in dirs.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var candidate = Path.Combine(dir.Trim('"'), name);
            if (File.Exists(candidate)) return candidate;
        }
        if (name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
            foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                var candidate = Path.Combine(dir, "Microsoft", "Edge", "Application", name);
                if (File.Exists(candidate)) return candidate;
            }
        throw new FileNotFoundException("未找到程序", name);
    }
}
