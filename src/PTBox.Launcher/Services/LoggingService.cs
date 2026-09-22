using System.IO;

namespace PTBox.Launcher.Services;

public sealed class LoggingService(string dataDirectory)
{
    private readonly object _sync = new();
    public string LogPath { get; } = Path.Combine(dataDirectory, "Logs", "launcher.log");
    public void Info(string message) => Write("INFO", message);
    public void Error(string message, Exception? exception = null) => Write("ERROR", $"{message} {exception}");
    private void Write(string level, string message)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 2 * 1024 * 1024)
                    File.Move(LogPath, LogPath + ".1", true);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Logging must never crash the UI. */ }
    }
}
