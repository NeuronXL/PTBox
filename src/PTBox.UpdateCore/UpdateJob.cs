using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace PTBox.UpdateCore;

public sealed record UpdateJob(string Id, string InstallDirectory, int ParentPid, long ParentStartTicks, string CurrentVersion);
public sealed record UpdateResult(string State, string Message, DateTimeOffset At);

public static class UpdateJobs
{
    public const string AppId = "84089F79-568E-4B87-A01C-9FE490CAB973";
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PTBox", "Updates", "jobs");
    public static string DirectoryFor(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("更新任务编号无效。");
        return Path.Combine(Root, id);
    }
    public static void NoReparsePoints(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("自动更新暂不支持链接或重定向目录，请手动安装。");
    }
    public static void ValidateInstallation(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("安装路径无效。");
        path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        NoReparsePoints(path);
        var marker = Path.Combine(path, "ptbox.install");
        if (File.Exists(Path.Combine(path, "ptbox.portable")) || !File.Exists(marker) || File.ReadAllText(marker).Trim() != AppId)
            throw new InvalidDataException("当前是便携版或未识别的安装，请从 Releases 手动安装一次接入版。");
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{" + AppId + "}_is1");
        if (key?.GetValue("InstallLocation") is not string registered || !Path.GetFullPath(registered).TrimEnd('\\').Equals(path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("安装位置与当前用户安装记录不一致，请手动升级。");
        var probe = Path.Combine(path, ".update-probe-" + Guid.NewGuid().ToString("N"));
        using (File.Create(probe)) { } File.Delete(probe);
    }
    public static UpdateJob Read(string id)
    {
        var directory = DirectoryFor(id); NoReparsePoints(directory);
        var path = Path.Combine(directory, "job.json");
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("更新任务过大。");
        var job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllBytes(path), UpdateProtocol.Json) ?? throw new InvalidDataException("更新任务为空。");
        if (job.Id != id) throw new InvalidDataException("任务编号不一致。");
        UpdateProtocol.ParseVersion(job.CurrentVersion);
        return job;
    }
    public static Process ValidateParent(UpdateJob job)
    {
        var process = Process.GetProcessById(job.ParentPid);
        try
        {
            if (process.StartTime.ToUniversalTime().Ticks != job.ParentStartTicks ||
                !string.Equals(process.MainModule?.FileName, Path.Combine(job.InstallDirectory, "PTBox.Launcher.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("发起更新的进程身份不一致。");
            var installedVersion = FileVersionInfo.GetVersionInfo(Path.Combine(job.InstallDirectory, "PTBox.Launcher.exe")).ProductVersion?.Split('+')[0];
            if (installedVersion != job.CurrentVersion) throw new InvalidDataException("当前安装版本与更新任务不一致。");
            return process;
        }
        catch { process.Dispose(); throw; }
    }
    public static ProcessStartInfo SetupStart(string package, string install, string log)
    {
        var info = new ProcessStartInfo(package) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(package)! };
        foreach (var value in new[] { "/SP-", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/NOCLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/DIR=" + install, "/LOG=" + log }) info.ArgumentList.Add(value);
        return info;
    }
    public static void Result(string id, string state, string message) => UpdateProtocol.WriteJson(Path.Combine(DirectoryFor(id), "result.json"), new UpdateResult(state, message, DateTimeOffset.UtcNow));
    public static void AcknowledgeStartup(string id, string installation)
    {
        var job = Read(id);
        if (!Path.GetFullPath(job.InstallDirectory).Equals(Path.GetFullPath(installation).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
        var folder = DirectoryFor(id);
        var m = UpdateProtocol.VerifyManifest(File.ReadAllBytes(Path.Combine(folder, "update.json")), File.ReadAllBytes(Path.Combine(folder, "update.json.sig")), ReleaseTrust.Embedded());
        if (m.Version == UpdateProtocol.AppVersion && File.Exists(Path.Combine(folder, "go")))
            File.WriteAllText(Path.Combine(folder, "started"), UpdateProtocol.AppVersion);
    }
}
