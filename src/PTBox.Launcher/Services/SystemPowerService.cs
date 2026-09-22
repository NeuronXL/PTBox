using System.Diagnostics;
using System.IO;

namespace PTBox.Launcher.Services;

public sealed class SystemPowerService(LoggingService log)
{
    // Call only after the modal confirmation returns true. Never use /f.
    public void Execute(bool restart)
    {
        log.Info(restart ? "用户确认重启" : "用户确认关机");
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), restart ? "/r /t 0" : "/s /t 0")
        { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
    }
}
