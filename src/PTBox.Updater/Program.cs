using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PTBox.UpdateCore;

namespace PTBox.Updater;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new Application(); var window = new UpdaterWindow();
        window.Loaded += async (_, _) =>
        {
            string? id = null;
            try
            {
                if (args.Length != 1) throw new InvalidDataException("请从 PTBox 的更新页面启动更新。");
                id = args[0]; await ExecuteAsync(id, window.Report);
                window.Complete();
            }
            catch (Exception ex)
            {
                var message = ex is TimeoutException ? "等待超时，升级未能完成。" : ex.Message;
                string? folder = null;
                try { if (id != null) { folder = UpdateJobs.DirectoryFor(id); UpdateJobs.Result(id, "Failed", message); File.WriteAllText(Path.Combine(folder, "failure.log"), ex.ToString()); } } catch { }
                window.ShowFailure(message + "\n\n请保留配置，用官方安装包重新安装修复。" + (folder == null ? "" : "\n日志：" + folder));
            }
        };
        app.Run(window);
    }
    private static async Task ExecuteAsync(string id, Action<string> status)
    {
        using var mutex = new Mutex(false, "Local\\PTBox.Update." + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
        bool locked; try { locked = mutex.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
        if (!locked) throw new InvalidOperationException("已有更新正在运行。");
        try
        {
            var job = UpdateJobs.Read(id); var folder = UpdateJobs.DirectoryFor(id);
            var runner = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\');
            if (!runner.Equals(Path.Combine(folder, "runner"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("更新程序必须从独立暂存目录运行。");
            UpdateJobs.ValidateInstallation(job.InstallDirectory);
            var m = UpdateProtocol.VerifyManifest(File.ReadAllBytes(Path.Combine(folder, "update.json")), File.ReadAllBytes(Path.Combine(folder, "update.json.sig")), ReleaseTrust.Embedded());
            UpdateProtocol.CheckCompatibility(m, job.CurrentVersion, Environment.OSVersion.Version.Build);
            using var parent = UpdateJobs.ValidateParent(job);
            await using var package = await UpdateProtocol.OpenVerifiedPackageAsync(Path.Combine(folder, "package.exe"), m);
            UpdateJobs.Result(id, "Prepared", "验证通过，等待 PTBox 退出。");
            File.WriteAllText(Path.Combine(folder, "ready"), "ready");
            status("安装包已验证，等待 PTBox 保存设置并退出…");
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            if (!File.Exists(Path.Combine(folder, "go")) || File.Exists(Path.Combine(folder, "cancel"))) throw new InvalidOperationException("本次更新已取消，未执行安装。");
            UpdateJobs.ValidateInstallation(job.InstallDirectory);
            UpdateJobs.Result(id, "Installing", "正在安装 " + m.Version);
            status("正在安装 " + m.Version + "，请稍候…");
            using var setup = Process.Start(UpdateJobs.SetupStart(package.Name, job.InstallDirectory, Path.Combine(folder, "setup.log"))) ?? throw new IOException("无法启动安装程序。");
            // Never kill an in-flight installer or start a second one on timeout.
            await setup.WaitForExitAsync();
            if (setup.ExitCode != 0) throw new IOException($"安装程序返回 {setup.ExitCode}。请查看 setup.log，未自动重启 Windows。");
            status("安装完成，正在启动新版…");
            var info = new ProcessStartInfo(Path.Combine(job.InstallDirectory, "PTBox.Launcher.exe")) { UseShellExecute = false, WorkingDirectory = job.InstallDirectory };
            info.ArgumentList.Add("--update-complete"); info.ArgumentList.Add(id);
            using var child = Process.Start(info) ?? throw new IOException("安装完成，但新版无法启动。");
            var timer = Stopwatch.StartNew();
            while (!File.Exists(Path.Combine(folder, "started")))
            {
                if (child.HasExited || timer.Elapsed > TimeSpan.FromSeconds(60)) throw new IOException("安装完成，但未收到新版启动确认。请手动打开 PTBox 或重新安装修复。");
                await Task.Delay(250);
            }
            UpdateJobs.Result(id, "Completed", "已更新到 " + m.Version);
        }
        finally { mutex.ReleaseMutex(); }
    }
}
