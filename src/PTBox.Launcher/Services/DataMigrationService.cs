using System.IO;
using System.Security.Cryptography;
using System.Text;
using PTBox.Launcher.Models;
using PTBox.UpdateCore;

namespace PTBox.Launcher.Services;

public static class DataMigrationService
{
    public const string InstallMarker = "ptbox.install";
    public const string AppId = "84089F79-568E-4B87-A01C-9FE490CAB973";
    public static string UserDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PTBox");
    public static bool IsInstalled(string directory) => !File.Exists(Path.Combine(directory, "ptbox.portable")) &&
        File.Exists(Path.Combine(directory, InstallMarker)) && File.ReadAllText(Path.Combine(directory, InstallMarker)).Trim() == AppId;

    // Both locations are explicit so migration can be exercised without touching the real user's data.
    public static string Migrate(string installation, string userDirectory, Func<string, string, bool> useLegacy)
    {
        var source = Path.Combine(installation, "Config", "config.json");
        var destination = Path.Combine(userDirectory, "Config", "config.json");
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return userDirectory;
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(installation).ToUpperInvariant())))[..16];
        var marker = Path.Combine(userDirectory, "Migrations", id + ".json");
        if (File.Exists(marker)) return userDirectory;
        if (!File.Exists(source)) return userDirectory;
        var original = File.ReadAllText(source); var config = ConfigService.Parse(original);
        if (File.Exists(destination))
        {
            var target = File.ReadAllText(destination); ConfigService.Parse(target);
            if (target == original || !useLegacy(source, destination))
            {
                Backup(source, userDirectory, "legacy-" + id);
                UpdateProtocol.WriteJson(marker, new { choice = "existing", source }); return userDirectory;
            }
            Backup(destination, userDirectory, "existing-" + id);
        }
        Backup(source, userDirectory, "legacy-" + id);
        var resources = Path.Combine(userDirectory, "Resources", id);
        string MigrateImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(Environment.ExpandEnvironmentVariables(path))) return path;
            var absolute = PathService.ResolveAsset(path, installation);
            if (!File.Exists(absolute)) return absolute; // Preserve the same missing-file fallback, never invent a new location.
            Directory.CreateDirectory(resources);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(absolute)))[..16];
            var copy = Path.Combine(resources, hash + Path.GetExtension(absolute));
            File.Copy(absolute, copy, true); return copy;
        }
        config.Background = MigrateImage(config.Background);
        foreach (var app in config.Apps)
        {
            app.Icon = MigrateImage(app.Icon);
            var path = Environment.ExpandEnvironmentVariables(app.Path);
            if (app.Type == "exe" && !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && (path.Contains('/') || path.Contains('\\')))
                app.Path = Path.GetFullPath(path, installation);
            if (!string.IsNullOrWhiteSpace(app.WorkingDirectory))
                app.WorkingDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(app.WorkingDirectory), installation);
        }
        ConfigService.Validate(config);
        new ConfigService(userDirectory, new LoggingService(userDirectory)).Save(config);
        UpdateProtocol.WriteJson(marker, new { choice = "migrated", source, at = DateTimeOffset.UtcNow });
        return userDirectory;
    }
    private static void Backup(string path, string root, string name)
    {
        var directory = Path.Combine(root, "Backups"); Directory.CreateDirectory(directory);
        File.Copy(path, Path.Combine(directory, name + "-" + Guid.NewGuid().ToString("N") + ".json"));
    }
}
