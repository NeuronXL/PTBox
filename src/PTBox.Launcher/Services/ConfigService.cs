using System.IO;
using System.Text.Json;
using PTBox.Launcher.Models;

namespace PTBox.Launcher.Services;

public sealed class ConfigService(string dataDirectory, LoggingService log)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true, WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true
    };
    public string ConfigPath { get; } = Path.Combine(dataDirectory, "Config", "config.json");
    public string? LoadWarning { get; private set; }
    public bool IsReadOnly { get; private set; }
    public static string ChooseDataDirectory()
    {
        if (DataMigrationService.IsInstalled(AppContext.BaseDirectory)) return DataMigrationService.UserDirectory;
        // Portable by default; installed under Program Files falls back to the user's profile.
        try
        {
            var probe = Path.Combine(AppContext.BaseDirectory, $".write-{Guid.NewGuid():N}");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return AppContext.BaseDirectory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PTBox");
        }
    }
    public LauncherConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                var bundled = Path.Combine(AppContext.BaseDirectory, "Config", "config.json");
                var seed = File.Exists(bundled) ? Parse(File.ReadAllText(bundled)) : DefaultConfig();
                Save(seed);
                return seed;
            }
            return Parse(File.ReadAllText(ConfigPath));
        }
        catch (UnsupportedConfigVersionException)
        {
            IsReadOnly = true;
            LoadWarning = "配置来自更高版本，已禁止写回。请安装对应新版；原配置未改动。";
            return DefaultConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log.Error("配置读取失败，使用默认配置", ex);
            LoadWarning = "配置无法读取，已使用默认配置。原配置会保留为 .broken 文件。";
            try
            {
                if (File.Exists(ConfigPath)) File.Copy(ConfigPath, ConfigPath + $".{DateTime.Now:yyyyMMdd-HHmmss}.broken", true);
                var defaults = DefaultConfig();
                Save(defaults);
                return defaults;
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
            {
                log.Error("配置备份或恢复写入失败", backupError);
                return DefaultConfig();
            }
        }
    }
    public static LauncherConfig Parse(string json)
    {
        var result = JsonSerializer.Deserialize<LauncherConfig>(json, JsonOptions)
            ?? throw new InvalidDataException("配置不能为空");
        Validate(result);
        return result;
    }
    public static void Validate(LauncherConfig config)
    {
        if (config.Version != 1) throw new UnsupportedConfigVersionException();
        if (config.Apps is null || config.Apps.Count > 99) throw new InvalidDataException("应用数须在 0–99 之间");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in config.Apps)
        {
            if (app is null || string.IsNullOrWhiteSpace(app.Id) || app.Id.StartsWith('$') || !ids.Add(app.Id))
                throw new InvalidDataException("应用 ID 必须唯一，且不能以 $ 开头");
            if (string.IsNullOrWhiteSpace(app.Name)) throw new InvalidDataException("应用名称不能为空");
            if (app.Type is not ("exe" or "url" or "uri")) throw new InvalidDataException($"{app.Name}：类型应为 exe / url / uri");
            if (app.LaunchBehavior is not ("waitForExit" or "fireAndForget")) throw new InvalidDataException("无效的启动行为");
            app.Subtitle ??= ""; app.Path ??= ""; app.Arguments ??= ""; app.Icon ??= ""; app.Accent ??= "#234B58";
            if (app.Category is not ("auto" or "games" or "streaming" or "media" or "apps")) app.Category = "auto";
            if (app.Type != "exe" && app.LaunchBehavior == "waitForExit")
                app.LaunchBehavior = "fireAndForget";
        }
        config.Theme = config.Theme == "Charcoal" ? "Charcoal" : "Midnight";
        config.Background ??= "";
        config.LastSelectedTile ??= "";
    }
    public void Save(LauncherConfig config)
    {
        if (IsReadOnly) throw new UnsupportedConfigVersionException();
        if (File.Exists(ConfigPath))
        {
            try
            {
                using var existing = JsonDocument.Parse(File.ReadAllText(ConfigPath), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (existing.RootElement.ValueKind == JsonValueKind.Object)
                    foreach (var field in existing.RootElement.EnumerateObject())
                        if (field.Name.Equals("version", StringComparison.OrdinalIgnoreCase) && field.Value.TryGetInt32(out var version) && version != 1)
                            throw new UnsupportedConfigVersionException();
            }
            catch (JsonException) { /* Load has already retained a broken-file backup when recovering. */ }
        }
        Validate(config);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var temporary = ConfigPath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(config, JsonOptions));
            File.Move(temporary, ConfigPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static LauncherConfig DefaultConfig() => new();
}

public sealed class UnsupportedConfigVersionException() : Exception("配置版本不受支持，已禁止覆盖；请使用对应版本的 PTBox。");
